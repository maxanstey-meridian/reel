package com.max.reel.data

import io.ktor.client.HttpClient
import io.ktor.client.engine.mock.MockEngine
import io.ktor.client.engine.mock.respond
import io.ktor.client.plugins.contentnegotiation.ContentNegotiation
import io.ktor.http.ContentType
import io.ktor.http.HttpHeaders
import io.ktor.http.HttpStatusCode
import io.ktor.http.headersOf
import io.ktor.serialization.kotlinx.json.json
import kotlinx.coroutines.test.runTest
import kotlinx.serialization.encodeToString
import kotlinx.serialization.json.Json
import org.junit.Assert.assertEquals
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
    private val jsonHeaders = headersOf(HttpHeaders.ContentType, ContentType.Application.Json.toString())

    private fun prefs() = PreferencesManager(RuntimeEnvironment.getApplication()).also {
        it.savePairing("localhost", "8080", "test-key", "Test server")
    }

    @Test
    fun `playback session decodes exact camelCase wire contract and ignores additions`() = runTest {
        val engine = MockEngine {
            respond(
                """{"session":{"sessionId":"session-1","revision":7,"createdAtUtc":"2026-08-11T10:00:00Z","updatedAtUtc":"2026-08-11T10:00:01Z","intent":{"relativePath":"Films/movie.mp4","desiredState":"playing","subtitleIndex":2,"pendingSeek":{"seekId":4,"positionSeconds":12.5,"introducedRevision":7}},"observation":null,"tvConnected":false,"futureField":true}}""",
                HttpStatusCode.OK,
                jsonHeaders,
            )
        }
        val client = HttpClient(engine) {
            install(ContentNegotiation) { json(json) }
            expectSuccess = false
        }

        val result = ApiClient(prefs(), client).getPlaybackSession() as ApiResult.Success

        assertEquals(PlaybackDesiredState.Playing, result.value.session?.intent?.desiredState)
        assertEquals(4L, result.value.session?.intent?.pendingSeek?.seekId)
        assertEquals(7L, result.value.session?.intent?.pendingSeek?.introducedRevision)
    }

    @Test
    fun `observation encodes camelCase enum values`() {
        val encoded = json.encodeToString(
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
    fun `non-success playback response retains status and server message`() = runTest {
        val engine = MockEngine {
            respond(
                """{"error":"Session is stale"}""",
                HttpStatusCode.Conflict,
                jsonHeaders,
            )
        }
        val client = HttpClient(engine) {
            install(ContentNegotiation) { json(json) }
            expectSuccess = false
        }

        val result = ApiClient(prefs(), client).getPlaybackSession() as ApiResult.Failure

        assertEquals(409, result.statusCode)
        assertEquals("Session is stale", result.message)
    }
}
