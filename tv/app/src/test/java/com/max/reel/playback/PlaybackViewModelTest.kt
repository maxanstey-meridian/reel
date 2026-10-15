package com.max.reel.playback

import androidx.media3.common.Player
import com.max.reel.data.ApiClient
import com.max.reel.data.DiscoveredServer
import com.max.reel.data.PairedServer
import com.max.reel.data.PairingStore
import com.max.reel.data.PlaybackObservationState
import com.max.reel.data.ReportPlaybackObservationRequest
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
import kotlinx.serialization.json.Json
import org.junit.After
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test
import org.junit.runner.RunWith
import org.robolectric.RobolectricTestRunner
import org.robolectric.RuntimeEnvironment
import org.robolectric.annotation.Config
import java.io.IOException

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
    private var sessionStatus = HttpStatusCode.OK
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

    private fun TestScope.startPolling() {
        pairing.save(paired)
        val mock =
            MockEngine { request ->
                if (request.url.host == unreachableHost) throw IOException("Connection refused")
                when (request.url.encodedPath) {
                    "/api/playback/session" -> {
                        respondJson(
                            """{"session":{"sessionId":"s1","revision":1,"intent":{"relativePath":"Films/movie.mp4","title":"Movie","desiredState":"playing"}}}""",
                            sessionStatus,
                        )
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

    private fun TestScope.assertInterruptedLoadStaysStopped(interrupt: (PlaybackViewModel) -> Unit) {
        streamInfoGate = CompletableDeferred()
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
