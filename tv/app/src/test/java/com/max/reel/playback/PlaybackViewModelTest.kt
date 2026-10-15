package com.max.reel.playback

import android.os.Looper
import androidx.media3.common.Player
import com.max.reel.data.ApiClient
import com.max.reel.data.DiscoveredServer
import com.max.reel.data.PairedServer
import com.max.reel.data.PairingStore
import com.max.reel.data.PlaybackDesiredState
import com.max.reel.data.PlaybackIntentResponse
import com.max.reel.data.PlaybackObservationState
import com.max.reel.data.PlaybackSessionResponse
import com.max.reel.data.ProgressRequest
import com.max.reel.data.ReportPlaybackObservationRequest
import com.max.reel.data.SeekRequestResponse
import com.max.reel.data.UpdatePlaybackIntentRequest
import io.ktor.client.engine.HttpClientEngineBase
import io.ktor.client.engine.mock.MockEngine
import io.ktor.client.engine.mock.MockRequestHandleScope
import io.ktor.client.engine.mock.respond
import io.ktor.client.request.HttpRequestData
import io.ktor.http.ContentType
import io.ktor.http.HttpHeaders
import io.ktor.http.HttpStatusCode
import io.ktor.http.content.TextContent
import io.ktor.http.headersOf
import io.ktor.util.InternalAPI
import kotlinx.coroutines.CompletableDeferred
import kotlinx.coroutines.CoroutineDispatcher
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.emptyFlow
import kotlinx.coroutines.flow.flowOf
import kotlinx.coroutines.launch
import kotlinx.coroutines.test.StandardTestDispatcher
import kotlinx.coroutines.test.TestScope
import kotlinx.coroutines.test.advanceTimeBy
import kotlinx.coroutines.test.resetMain
import kotlinx.coroutines.test.runCurrent
import kotlinx.coroutines.test.runTest
import kotlinx.coroutines.test.setMain
import kotlinx.serialization.encodeToString
import kotlinx.serialization.json.Json
import org.junit.After
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNotNull
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Assert.fail
import org.junit.Test
import org.junit.runner.RunWith
import org.robolectric.RobolectricTestRunner
import org.robolectric.RuntimeEnvironment
import org.robolectric.Shadows.shadowOf
import org.robolectric.annotation.Config
import java.io.IOException
import java.time.Duration

@RunWith(RobolectricTestRunner::class)
@Config(sdk = [34])
class PlaybackViewModelTest {
    private val dispatcher = StandardTestDispatcher().also { Dispatchers.setMain(it) }
    private val app = RuntimeEnvironment.getApplication()
    private val pairing = PairingStore(app)
    private val paired = PairedServer("127.0.0.1", 9, "key", "Test")
    private var unreachableHost: String? = null
    private var discovered: Flow<DiscoveredServer> = emptyFlow()
    private val json = Json { ignoreUnknownKeys = true }
    private val observations = mutableListOf<ReportPlaybackObservationRequest>()
    private val progress = mutableListOf<ProgressRequest>()
    private var session = session()
    private var sessionStatus = HttpStatusCode.OK
    private var progressStatus = HttpStatusCode.OK
    private var unreachablePath: String? = null
    private var streamInfoGate: CompletableDeferred<Unit>? = null
    private var observationGate: CompletableDeferred<Unit>? = null
    private lateinit var viewModel: PlaybackViewModel

    @After
    fun tearDown() {
        viewModel.player.release()
        Dispatchers.resetMain()
    }

    private fun MockRequestHandleScope.respondJson(body: String, status: HttpStatusCode = HttpStatusCode.OK) = respond(
        body,
        status,
        headersOf(HttpHeaders.ContentType, ContentType.Application.Json.toString()),
    )

    private fun session(
        id: String = "s1",
        revision: Long = 1,
        path: String = "Films/movie.mp4",
        desiredState: PlaybackDesiredState = PlaybackDesiredState.Playing,
        seekSeconds: Double? = null,
    ) = PlaybackSessionResponse(
        id,
        revision,
        PlaybackIntentResponse(
            relativePath = path,
            title = "Movie",
            desiredState = desiredState,
            pendingSeek = seekSeconds?.let { SeekRequestResponse(revision, it, revision) },
        ),
    )

    private fun TestScope.startPolling() {
        pairing.save(paired)
        val mock =
            MockEngine { request ->
                if (request.url.host == unreachableHost || request.url.encodedPath == unreachablePath) {
                    throw IOException("Connection refused")
                }
                when (request.url.encodedPath) {
                    "/api/playback/session" -> {
                        respondJson("""{"session":${json.encodeToString(session)}}""", sessionStatus)
                    }

                    "/api/playback/session/intent" -> {
                        val intent =
                            json.decodeFromString<UpdatePlaybackIntentRequest>((request.body as TextContent).text)
                        session =
                            session(
                                revision = session.revision + 1,
                                path = session.intent.relativePath,
                                desiredState = intent.desiredState,
                                seekSeconds = intent.seekPositionSeconds,
                            )
                        respondJson(json.encodeToString(session))
                    }

                    "/api/progress" -> {
                        progress += json.decodeFromString<ProgressRequest>((request.body as TextContent).text)
                        respondJson("{}", progressStatus)
                    }

                    "/api/files/stream/info" -> {
                        streamInfoGate?.await()
                        respondJson("""{"durationSeconds":60,"transcodeHeight":null}""")
                    }

                    "/api/files/subtitles" -> {
                        respondJson("[]")
                    }

                    "/api/playback/session/observation" -> {
                        observations +=
                            json.decodeFromString<ReportPlaybackObservationRequest>(
                                (request.body as TextContent).text,
                            )
                        observationGate?.await()
                        respondJson("{}")
                    }

                    else -> {
                        respondJson("{}")
                    }
                }
            }
        viewModel =
            PlaybackViewModel(
                app,
                pairing,
                ApiClient(pairing, SameThreadEngine(dispatcher, mock)),
            ) {
                discovered
            }
        backgroundScope.launch { viewModel.poll() }
        runCurrent()
    }

    @Test
    fun `stop reported while another observation is in flight is still sent`() = runTest(dispatcher) {
        observationGate = CompletableDeferred()
        startPolling()
        assertEquals(PlaybackObservationState.Loading, observations.single().state)

        viewModel.stopFromTv()
        observationGate!!.complete(Unit)
        runCurrent()

        assertEquals(PlaybackObservationState.Idle, observations.last().state)
        assertTrue(
            observations.last().observationSequence > observations.first().observationSequence,
        )
    }

    @Test
    fun `rejected key stops playback and unpairs`() = runTest(dispatcher) {
        startPolling()
        assertEquals("Films/movie.mp4", viewModel.state.value.relativePath)

        sessionStatus = HttpStatusCode.Unauthorized
        advanceTimeBy(1_001)

        assertNull(pairing.server.value)
        assertNull(viewModel.state.value.relativePath)
        assertEquals(Player.STATE_IDLE, viewModel.player.playbackState)
    }

    @Test
    fun `a moved server is found again by its key and polling resumes there`() = runTest(dispatcher) {
        startPolling()
        discovered =
            flowOf(
                DiscoveredServer("10.0.0.7", 8080, "other-key"),
                DiscoveredServer("10.0.0.9", 8081, "key"),
            )
        unreachableHost = "127.0.0.1"

        advanceTimeBy(1_000 + 1_000 + 2_000 + 1)

        assertEquals(paired.copy(host = "10.0.0.9", port = 8081), pairing.server.value)
        assertEquals(PlaybackConnection.Connected, viewModel.state.value.connection)
    }

    @Test
    fun `a refusing server is not rediscovered and keeps the pairing`() = runTest(dispatcher) {
        startPolling()
        discovered = flowOf(DiscoveredServer("10.0.0.9", 8081, "key"))
        sessionStatus = HttpStatusCode.Forbidden

        advanceTimeBy(1_000 + 1_000 + 2_000 + 4_000 + 1)

        assertEquals(paired, pairing.server.value)
        assertEquals(PlaybackConnection.Disconnected, viewModel.state.value.connection)
        assertEquals(
            "The server only accepts devices on its own network",
            viewModel.state.value.connectionError,
        )
    }

    @Test
    fun `back during an in-flight load leaves nothing playing`() = runTest(dispatcher) {
        assertInterruptedLoadStaysStopped { it.stopFromTv() }
    }

    @Test
    fun `backgrounding during an in-flight load leaves nothing playing`() = runTest(dispatcher) {
        assertInterruptedLoadStaysStopped { it.pauseForBackground() }
    }

    @Test
    fun `the first observation of a new session carries that session's revision`() = runTest(dispatcher) {
        session = session(revision = 3)
        startPolling()
        assertEquals(3L, observations.first().appliedRevision)

        session = session(id = "s2", revision = 1, path = "Films/other.mp4")
        advanceTimeBy(1_001)

        val first = observations.first { it.sessionId == "s2" }
        assertEquals(1L, first.appliedRevision)
        assertEquals("Films/other.mp4", first.relativePath)
    }

    @Test
    fun `an unreachable stream info loads nothing and is retried on the next poll`() = runTest(dispatcher) {
        assertUnreachableLoadIsRetried("/api/files/stream/info")
    }

    @Test
    fun `an unreachable subtitle list loads nothing and is retried on the next poll`() = runTest(dispatcher) {
        assertUnreachableLoadIsRetried("/api/files/subtitles")
    }

    private fun TestScope.assertUnreachableLoadIsRetried(path: String) {
        unreachablePath = path
        startPolling()

        assertNull(viewModel.state.value.relativePath)
        assertEquals(0, viewModel.player.mediaItemCount)
        assertTrue(observations.isEmpty())

        unreachablePath = null
        advanceTimeBy(1_001)

        assertEquals("Films/movie.mp4", viewModel.state.value.relativePath)
        assertEquals(1, viewModel.player.mediaItemCount)
    }

    @Test
    fun `switching files saves the old file's position before the new one loads`() = runTest(dispatcher) {
        session = session(seekSeconds = 30.0)
        startPolling()

        session = session(id = "s2", revision = 2, path = "Films/other.mp4")
        advanceTimeBy(1_001)

        assertEquals(ProgressRequest("Films/movie.mp4", 30.0, 60.0), progress.first())
        assertEquals("Films/other.mp4", viewModel.state.value.relativePath)
    }

    @Test
    fun `a backwards seek while paused saves the new position`() = runTest(dispatcher) {
        session = session(desiredState = PlaybackDesiredState.Paused, seekSeconds = 30.0)
        startPolling()

        viewModel.skip(-10.0)
        runCurrent()

        assertEquals(ProgressRequest("Films/movie.mp4", 20.0, 60.0), progress.last())
    }

    @Test
    fun `a failing progress save does not stop controls`() = runTest(dispatcher) {
        progressStatus = HttpStatusCode.InternalServerError
        session = session(desiredState = PlaybackDesiredState.Paused, seekSeconds = 30.0)
        startPolling()
        assertTrue(progress.isNotEmpty())

        viewModel.togglePlayPause()
        runCurrent()

        assertTrue(viewModel.player.playWhenReady)
        assertFalse(viewModel.state.value.controlPending)
        assertNull(viewModel.state.value.notice)
    }

    @Test
    fun `a phone play after a playback error prepares the file again`() = runTest(dispatcher) {
        startPolling()
        awaitPlayerError()
        assertNotNull(viewModel.state.value.error)

        session = session(revision = 2)
        advanceTimeBy(1_001)

        assertNull(viewModel.player.playerError)
        assertNull(viewModel.state.value.error)
    }

    // Nothing listens on the paired port, so the stream fails once the player's clock lets it retry.
    private fun awaitPlayerError() {
        repeat(200) {
            if (viewModel.player.playerError != null) return
            shadowOf(Looper.getMainLooper()).idleFor(Duration.ofMillis(500))
            Thread.sleep(5)
        }
        fail("The player never reported an error")
    }

    // The intent is unreachable so that only the interrupted load could still touch the player.
    private fun TestScope.assertInterruptedLoadStaysStopped(interrupt: (PlaybackViewModel) -> Unit) {
        streamInfoGate = CompletableDeferred()
        unreachablePath = "/api/playback/session/intent"
        startPolling()

        interrupt(viewModel)
        streamInfoGate!!.complete(Unit)
        runCurrent()

        assertNull(viewModel.state.value.relativePath)
        assertEquals(0, viewModel.player.mediaItemCount)
        assertFalse(viewModel.player.playWhenReady)
    }
}

// Ktor 2's MockEngine always answers on Dispatchers.IO (a configurable engine dispatcher arrives in
// Ktor 3); this keeps every request on the test scheduler so runCurrent() settles them.
private class SameThreadEngine(override val dispatcher: CoroutineDispatcher, private val mock: MockEngine) :
    HttpClientEngineBase("same-thread") {
    override val config get() = mock.config
    override val supportedCapabilities get() = mock.supportedCapabilities

    @InternalAPI
    override suspend fun execute(data: HttpRequestData) = mock.execute(data)
}
