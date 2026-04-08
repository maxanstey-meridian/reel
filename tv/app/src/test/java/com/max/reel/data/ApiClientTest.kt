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

class ApiClientTest {

    private fun createTestClient(
        handler: suspend (io.ktor.client.engine.mock.MockRequestHandleScope.(io.ktor.http.Url) -> io.ktor.client.request.HttpResponseData),
    ): HttpClient {
        val mockEngine = MockEngine { request ->
            handler(request.url)
        }
        return HttpClient(mockEngine) {
            install(ContentNegotiation) {
                json(Json { ignoreUnknownKeys = true })
            }
            expectSuccess = false
        }
    }

    private val jsonHeaders = headersOf(HttpHeaders.ContentType, ContentType.Application.Json.toString())

    // --- fetchPosters ---

    @Test
    fun `fetchPosters deserializes string array on 200`() = runTest {
        val client = createTestClient { url ->
            if (url.encodedPath == "/api/library/posters") {
                respond("""["Films/a.mp4","Films/b.mp4","Films/c.mp4"]""", HttpStatusCode.OK, jsonHeaders)
            } else {
                respond("", HttpStatusCode.NotFound)
            }
        }

        val apiClient = ApiClientTestable(client, "http://localhost:8080", "test-key")
        val result = apiClient.fetchPosters(3)

        assertEquals(listOf("Films/a.mp4", "Films/b.mp4", "Films/c.mp4"), result)
    }

    @Test
    fun `fetchPosters returns empty list on non-200`() = runTest {
        val client = createTestClient {
            respond("", HttpStatusCode.InternalServerError)
        }

        val apiClient = ApiClientTestable(client, "http://localhost:8080", "test-key")
        val result = apiClient.fetchPosters(5)

        assertEquals(emptyList<String>(), result)
    }

    @Test
    fun `fetchPosters returns empty list on network error`() = runTest {
        val mockEngine = MockEngine { throw java.io.IOException("Connection refused") }
        val client = HttpClient(mockEngine) {
            install(ContentNegotiation) {
                json(Json { ignoreUnknownKeys = true })
            }
            expectSuccess = false
        }

        val apiClient = ApiClientTestable(client, "http://localhost:8080", "test-key")
        val result = apiClient.fetchPosters(5)

        assertEquals(emptyList<String>(), result)
    }

    // --- fetchStats ---

    @Test
    fun `fetchStats deserializes LibraryStats on 200`() = runTest {
        val client = createTestClient { url ->
            if (url.encodedPath == "/api/library/stats") {
                respond("""{"totalFiles":42,"totalBytes":123456789}""", HttpStatusCode.OK, jsonHeaders)
            } else {
                respond("", HttpStatusCode.NotFound)
            }
        }

        val apiClient = ApiClientTestable(client, "http://localhost:8080", "test-key")
        val result = apiClient.fetchStats()

        assertEquals(LibraryStats(totalFiles = 42, totalBytes = 123456789), result)
    }

    @Test
    fun `fetchStats returns null on non-200`() = runTest {
        val client = createTestClient {
            respond("", HttpStatusCode.Unauthorized)
        }

        val apiClient = ApiClientTestable(client, "http://localhost:8080", "test-key")
        val result = apiClient.fetchStats()

        assertNull(result)
    }

    @Test
    fun `fetchStats returns null on network error`() = runTest {
        val mockEngine = MockEngine { throw java.io.IOException("Timeout") }
        val client = HttpClient(mockEngine) {
            install(ContentNegotiation) {
                json(Json { ignoreUnknownKeys = true })
            }
            expectSuccess = false
        }

        val apiClient = ApiClientTestable(client, "http://localhost:8080", "test-key")
        val result = apiClient.fetchStats()

        assertNull(result)
    }

    // --- fetchUsers ---

    @Test
    fun `fetchUsers deserializes user list on 200`() = runTest {
        val client = createTestClient { url ->
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

        val apiClient = ApiClientTestable(client, "http://localhost:8080", "test-key")
        val result = apiClient.fetchUsers()

        assertEquals(2, result.size)
        assertEquals("Max", result[0].name)
        assertEquals(1, result[0].id)
        assertNull(result[0].icon)
        assertEquals("Guest", result[1].name)
    }

    @Test
    fun `fetchUsers returns empty list on non-200`() = runTest {
        val client = createTestClient {
            respond("", HttpStatusCode.Forbidden)
        }

        val apiClient = ApiClientTestable(client, "http://localhost:8080", "test-key")
        val result = apiClient.fetchUsers()

        assertEquals(emptyList<UserInfo>(), result)
    }

    @Test
    fun `fetchUsers handles unknown fields gracefully`() = runTest {
        val client = createTestClient { url ->
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

        val apiClient = ApiClientTestable(client, "http://localhost:8080", "test-key")
        val result = apiClient.fetchUsers()

        assertEquals(1, result.size)
        assertEquals("Max", result[0].name)
    }

    @Test
    fun `fetchPosters sends auth header`() = runTest {
        var capturedAuthHeader: String? = null
        val mockEngine = MockEngine { request ->
            capturedAuthHeader = request.headers["X-Secret-Key"]
            respond("""[]""", HttpStatusCode.OK, jsonHeaders)
        }
        val client = HttpClient(mockEngine) {
            install(ContentNegotiation) {
                json(Json { ignoreUnknownKeys = true })
            }
            expectSuccess = false
        }

        val apiClient = ApiClientTestable(client, "http://localhost:8080", "my-secret")
        apiClient.fetchPosters(1)

        assertEquals("my-secret", capturedAuthHeader)
    }
}
