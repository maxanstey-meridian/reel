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
import kotlinx.serialization.json.Json
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Test
import org.junit.runner.RunWith
import org.robolectric.RobolectricTestRunner
import org.robolectric.RuntimeEnvironment
import org.robolectric.annotation.Config

@RunWith(RobolectricTestRunner::class)
@Config(sdk = [34])
class ApiClientTest {

    private val jsonHeaders = headersOf(HttpHeaders.ContentType, ContentType.Application.Json.toString())

    private fun createPrefs(): PreferencesManager {
        val prefs = PreferencesManager(RuntimeEnvironment.getApplication())
        prefs.savePairing("localhost", "8080", "test-key")
        return prefs
    }

    private fun createApiClient(
        handler: suspend io.ktor.client.engine.mock.MockRequestHandleScope.(io.ktor.http.Url) -> io.ktor.client.request.HttpResponseData,
    ): ApiClient {
        val mockEngine = MockEngine { request ->
            handler(request.url)
        }
        val mockClient = HttpClient(mockEngine) {
            install(ContentNegotiation) {
                json(Json { ignoreUnknownKeys = true })
            }
            expectSuccess = false
        }
        return ApiClient(createPrefs(), mockClient)
    }

    private fun createApiClientWithEngine(engine: MockEngine): ApiClient {
        val mockClient = HttpClient(engine) {
            install(ContentNegotiation) {
                json(Json { ignoreUnknownKeys = true })
            }
            expectSuccess = false
        }
        return ApiClient(createPrefs(), mockClient)
    }

    // --- fetchPosters ---

    @Test
    fun `fetchPosters deserializes string array on 200`() = runTest {
        val apiClient = createApiClient { url ->
            if (url.encodedPath == "/api/library/posters") {
                respond("""["Films/a.mp4","Films/b.mp4","Films/c.mp4"]""", HttpStatusCode.OK, jsonHeaders)
            } else {
                respond("", HttpStatusCode.NotFound)
            }
        }

        val result = apiClient.fetchPosters(3)
        assertEquals(listOf("Films/a.mp4", "Films/b.mp4", "Films/c.mp4"), result)
    }

    @Test
    fun `fetchPosters returns empty list on non-200`() = runTest {
        val apiClient = createApiClient {
            respond("", HttpStatusCode.InternalServerError)
        }

        val result = apiClient.fetchPosters(5)
        assertEquals(emptyList<String>(), result)
    }

    @Test
    fun `fetchPosters returns empty list on network error`() = runTest {
        val apiClient = createApiClientWithEngine(
            MockEngine { throw java.io.IOException("Connection refused") },
        )

        val result = apiClient.fetchPosters(5)
        assertEquals(emptyList<String>(), result)
    }

    // --- fetchStats ---

    @Test
    fun `fetchStats deserializes LibraryStats on 200`() = runTest {
        val apiClient = createApiClient { url ->
            if (url.encodedPath == "/api/library/stats") {
                respond("""{"totalFiles":42,"totalBytes":123456789}""", HttpStatusCode.OK, jsonHeaders)
            } else {
                respond("", HttpStatusCode.NotFound)
            }
        }

        val result = apiClient.fetchStats()
        assertEquals(LibraryStats(totalFiles = 42, totalBytes = 123456789), result)
    }

    @Test
    fun `fetchStats returns null on non-200`() = runTest {
        val apiClient = createApiClient {
            respond("", HttpStatusCode.Unauthorized)
        }

        val result = apiClient.fetchStats()
        assertNull(result)
    }

    @Test
    fun `fetchStats returns null on network error`() = runTest {
        val apiClient = createApiClientWithEngine(
            MockEngine { throw java.io.IOException("Timeout") },
        )

        val result = apiClient.fetchStats()
        assertNull(result)
    }

    // --- fetchUsers ---

    @Test
    fun `fetchUsers deserializes user list on 200`() = runTest {
        val apiClient = createApiClient { url ->
            if (url.encodedPath == "/api/users") {
                respond(
                    """[{"id":1,"name":"Max","icon":null},{"id":2,"name":"Guest"}]""",
                    HttpStatusCode.OK,
                    jsonHeaders,
                )
            } else {
                respond("", HttpStatusCode.NotFound)
            }
        }

        val result = apiClient.fetchUsers()
        assertEquals(2, result.size)
        assertEquals("Max", result[0].name)
        assertEquals(1, result[0].id)
        assertNull(result[0].icon)
        assertEquals("Guest", result[1].name)
    }

    @Test
    fun `fetchUsers returns empty list on non-200`() = runTest {
        val apiClient = createApiClient {
            respond("", HttpStatusCode.Forbidden)
        }

        val result = apiClient.fetchUsers()
        assertEquals(emptyList<UserInfo>(), result)
    }

    @Test
    fun `fetchUsers handles unknown fields gracefully`() = runTest {
        val apiClient = createApiClient { url ->
            if (url.encodedPath == "/api/users") {
                respond(
                    """[{"id":1,"name":"Max","icon":null,"extraField":"ignored"}]""",
                    HttpStatusCode.OK,
                    jsonHeaders,
                )
            } else {
                respond("", HttpStatusCode.NotFound)
            }
        }

        val result = apiClient.fetchUsers()
        assertEquals(1, result.size)
        assertEquals("Max", result[0].name)
    }

    @Test
    fun `fetchPosters sends auth header`() = runTest {
        var capturedAuthHeader: String? = null
        val apiClient = createApiClientWithEngine(
            MockEngine { request ->
                capturedAuthHeader = request.headers["X-Secret-Key"]
                respond("""[]""", HttpStatusCode.OK, jsonHeaders)
            },
        )

        apiClient.fetchPosters(1)
        assertEquals("test-key", capturedAuthHeader)
    }

    // --- nextFile ---

    @Test
    fun `nextFile returns response on 200`() = runTest {
        val apiClient = createApiClient { url ->
            if (url.encodedPath == "/api/files/next") {
                respond(
                    """{"relativePath":"Films/b.mp4","name":"b.mp4","type":"file"}""",
                    HttpStatusCode.OK,
                    jsonHeaders,
                )
            } else {
                respond("", HttpStatusCode.NotFound)
            }
        }

        val result = apiClient.nextFile("Films/a.mp4")
        assertEquals("Films/b.mp4", result?.relativePath)
    }

    @Test
    fun `nextFile returns null on 204`() = runTest {
        val apiClient = createApiClient {
            respond("", HttpStatusCode.NoContent)
        }

        val result = apiClient.nextFile("Films/last.mp4")
        assertNull(result)
    }

    @Test
    fun `nextFile returns null on network error`() = runTest {
        val apiClient = createApiClientWithEngine(
            MockEngine { throw java.io.IOException("Timeout") },
        )

        val result = apiClient.nextFile("Films/a.mp4")
        assertNull(result)
    }

    // --- fetchCastQueue ---

    @Test
    fun `fetchCastQueue returns paths on 200`() = runTest {
        val apiClient = createApiClient { url ->
            if (url.encodedPath == "/api/cast/queue") {
                respond("""["Films/b.mp4","Films/c.mp4"]""", HttpStatusCode.OK, jsonHeaders)
            } else {
                respond("", HttpStatusCode.NotFound)
            }
        }

        val result = apiClient.fetchCastQueue()
        assertEquals(listOf("Films/b.mp4", "Films/c.mp4"), result)
    }

    @Test
    fun `fetchCastQueue returns empty list on non-200`() = runTest {
        val apiClient = createApiClient {
            respond("", HttpStatusCode.InternalServerError)
        }

        val result = apiClient.fetchCastQueue()
        assertEquals(emptyList<String>(), result)
    }

    @Test
    fun `fetchCastQueue returns empty list on network error`() = runTest {
        val apiClient = createApiClientWithEngine(
            MockEngine { throw java.io.IOException("Timeout") },
        )

        val result = apiClient.fetchCastQueue()
        assertEquals(emptyList<String>(), result)
    }
}
