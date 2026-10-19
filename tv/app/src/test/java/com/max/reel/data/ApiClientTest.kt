package com.max.reel.data

import io.ktor.client.engine.mock.MockEngine
import io.ktor.client.engine.mock.MockRequestHandleScope
import io.ktor.client.engine.mock.respond
import io.ktor.client.plugins.HttpTimeout
import io.ktor.client.request.HttpRequestData
import io.ktor.client.request.HttpResponseData
import io.ktor.http.ContentType
import io.ktor.http.HttpHeaders
import io.ktor.http.HttpStatusCode
import io.ktor.http.headersOf
import kotlinx.coroutines.test.runTest
import kotlinx.serialization.encodeToString
import kotlinx.serialization.json.Json
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNotNull
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test
import org.junit.runner.RunWith
import org.robolectric.RobolectricTestRunner
import org.robolectric.RuntimeEnvironment
import org.robolectric.annotation.Config

@RunWith(RobolectricTestRunner::class)
@Config(sdk = [34])
class ApiClientTest {
    private val json = Json { ignoreUnknownKeys = true }
    private val server = PairedServer("localhost", 8080, "test-key", "Test server")
    private val pairing = PairingStore(RuntimeEnvironment.getApplication()).also { it.save(server) }

    private fun client(handler: suspend MockRequestHandleScope.(HttpRequestData) -> HttpResponseData) =
        ApiClient(pairing, MockEngine(handler))

    private fun MockRequestHandleScope.respondJson(body: String, status: HttpStatusCode = HttpStatusCode.OK) = respond(
        body,
        status,
        headersOf(HttpHeaders.ContentType, ContentType.Application.Json.toString()),
    )

    @Test
    fun `playback session decodes exact camelCase wire contract and ignores additions`() = runTest {
        val api =
            client {
                respondJson(
                    """{"session":{"sessionId":"session-1","revision":7,"intent":{"relativePath":"Films/movie.mp4","title":"Movie","desiredState":"playing","subtitleIndex":2,"pendingSeek":{"seekId":4,"positionSeconds":12.5}},"observation":null,"tvConnected":false,"futureField":true}}""",
                )
            }

        val session = (api.getPlaybackSession() as ApiResult.Success).value.session

        assertEquals("Movie", session?.intent?.title)
        assertEquals(PlaybackDesiredState.Playing, session?.intent?.desiredState)
        assertEquals(4L, session?.intent?.pendingSeek?.seekId)
    }

    @Test
    fun `stream urls carry the mode the server planned`() {
        val api = client { respondJson("{}") }

        assertEquals(
            "http://localhost:8080/api/files/stream?path=a.mkv",
            api.streamUrl(server, "a.mkv", StreamMode.Direct, null, 30.0),
        )
        assertEquals(
            "http://localhost:8080/api/files/stream?path=a.avi&remux=true&start=30.0",
            api.streamUrl(server, "a.avi", StreamMode.Remux, null, 30.0),
        )
        assertEquals(
            "http://localhost:8080/api/files/stream?path=a.mkv&height=480&start=30.0",
            api.streamUrl(server, "a.mkv", StreamMode.Transcode, 480, 30.0),
        )
    }

    @Test
    fun `urls bracket IPv6 hosts and encode the key`() {
        val api = client { respondJson("{}") }

        assertEquals(
            "http://[fe80::1]:8080/pair?key=a+%26+b",
            api.pairUrl(PairedServer("fe80::1", 8080, "a & b", "")),
        )
    }

    @Test
    fun `requests carry the paired server, key and encoded path`() = runTest {
        var seen: HttpRequestData? = null
        val api =
            client { request ->
                seen = request
                respondJson("""{"durationSeconds":60.5,"mode":"transcode","transcodeHeight":480}""")
            }

        val info = (api.streamInfo("Shows/A & B/ep 1.mkv", maxHeight = 480) as ApiResult.Success).value

        assertEquals(StreamInfo(60.5, StreamMode.Transcode, transcodeHeight = 480), info)
        assertEquals(
            "http://localhost:8080/api/files/stream/info?path=Shows%2FA+%26+B%2Fep+1.mkv&maxHeight=480",
            seen?.url.toString(),
        )
        assertEquals("test-key", seen?.headers?.get("X-Secret-Key"))
    }

    @Test
    fun `a rejected connect goes to the discovered server and keeps the current pairing`() = runTest {
        var seen: HttpRequestData? = null
        val api =
            client { request ->
                seen = request
                respondJson(
                    """{"code":"unauthorized","message":"That code doesn't match your TV."}""",
                    HttpStatusCode.Unauthorized,
                )
            }

        val result =
            api.connect(
                DiscoveredServer("192.168.1.9", 9090, "secret"),
            ) as ApiResult.Failure

        assertEquals(401, result.statusCode)
        assertEquals("http://192.168.1.9:9090/api/connect", seen?.url.toString())
        assertEquals(server, pairing.server.value)
    }

    @Test
    fun `rejected key unpairs`() = runTest {
        val api =
            client {
                respondJson(
                    """{"code":"unauthorized","message":"Missing or invalid secret key."}""",
                    HttpStatusCode.Unauthorized,
                )
            }

        api.startPlayback("Films/movie.mp4")

        assertNull(pairing.server.value)
    }

    @Test
    fun `a request outside the private network keeps the pairing and says why`() = runTest {
        val api = client { respond("", HttpStatusCode.Forbidden) }

        val result = api.getPlaybackSession() as ApiResult.Failure

        assertEquals(403, result.statusCode)
        assertEquals("The server only accepts devices on its own network", result.message)
        assertNotNull(pairing.server.value)
    }

    @Test
    fun `observation encodes camelCase enum values`() {
        val encoded =
            json.encodeToString(
                ReportPlaybackObservationRequest(
                    sessionId = "session-1",
                    observationSequence = 1,
                    appliedRevision = 3,
                    state = PlaybackObservationState.Buffering,
                    relativePath = "Films/movie.mp4",
                    positionSeconds = 5.0,
                    durationSeconds = 60.0,
                ),
            )

        assertTrue(encoded.contains("\"appliedRevision\":3"))
        assertTrue(encoded.contains("\"state\":\"buffering\""))
        assertTrue(encoded.contains("\"relativePath\":\"Films/movie.mp4\""))
    }

    @Test
    fun `intent always encodes subtitle and quality, and expected revision only when present`() {
        val guarded =
            UpdatePlaybackIntentRequest(
                "session-1",
                PlaybackDesiredState.Paused,
                subtitleIndex = 2,
                maxHeight = 480,
                expectedRevision = 3,
            )
        val unguarded = UpdatePlaybackIntentRequest("session-1", PlaybackDesiredState.Stopped, null, null)

        assertTrue(json.encodeToString(guarded).contains("\"expectedRevision\":3"))
        assertTrue(!json.encodeToString(unguarded).contains("expectedRevision"))
        assertTrue(json.encodeToString(unguarded).contains("\"subtitleIndex\":null,\"maxHeight\":null"))
    }

    @Test
    fun `non-success playback response retains status and server message`() = runTest {
        val api =
            client {
                respondJson(
                    """{"code":"conflict","message":"Session is stale"}""",
                    HttpStatusCode.Conflict,
                )
            }

        val result = api.getPlaybackSession() as ApiResult.Failure

        assertEquals(409, result.statusCode)
        assertEquals("Session is stale", result.message)
    }

    @Test
    fun `an undecodable success is an unexpected response that keeps its status`() = runTest {
        val malformed = client { respondJson("<html>Not Reel</html>") }
        val wrongType =
            client {
                respond("<html>Not Reel</html>", headers = headersOf(HttpHeaders.ContentType, "text/html"))
            }

        for (api in listOf(malformed, wrongType)) {
            val result = api.getPlaybackSession() as ApiResult.Failure

            assertEquals(200, result.statusCode)
            assertEquals("Unexpected response from the server", result.message)
        }
    }

    @Test
    fun `a server error without a Reel message reads as plain copy, not a status code`() = runTest {
        val api = client { respond("Bad gateway", HttpStatusCode.BadGateway) }

        val result = api.getPlaybackSession() as ApiResult.Failure

        assertEquals(502, result.statusCode)
        assertEquals("The Reel server isn't responding properly", result.message)
    }

    @Test
    fun `stream info with an unknown duration decodes it as null`() = runTest {
        val api = client { respondJson("""{"durationSeconds":null,"mode":"remux","transcodeHeight":null}""") }

        val info = (api.streamInfo("Films/movie.mp4", maxHeight = null) as ApiResult.Success).value

        assertNull(info.durationSeconds)
        assertEquals(StreamMode.Remux, info.mode)
    }

    @Test
    fun `stream info, which probes the file on the server, may take longer than other requests`() = runTest {
        val timeouts = mutableMapOf<String, Long?>()
        val api =
            client { request ->
                timeouts[request.url.encodedPath] = request.getCapabilityOrNull(HttpTimeout)?.requestTimeoutMillis
                respondJson("{}")
            }

        api.streamInfo("Films/movie.mp4", maxHeight = null)
        api.getPlaybackSession()

        assertEquals(30_000L, timeouts["/api/files/stream/info"])
        assertEquals(5_000L, timeouts["/api/playback/session"])
    }
}
