package com.max.reel.data

import io.ktor.client.HttpClient
import io.ktor.client.call.body
import io.ktor.client.request.get
import io.ktor.client.request.header

/**
 * Test-only version of ApiClient that accepts an injected HttpClient.
 * Mirrors the same HTTP + deserialization logic as the production ApiClient
 * so we can verify response parsing with a mock engine.
 */
class ApiClientTestable(
    private val client: HttpClient,
    private val baseUrl: String,
    private val secretKey: String,
) {
    suspend fun fetchPosters(count: Int = 20): List<String> {
        return try {
            val response = client.get("$baseUrl/api/library/posters?count=$count") {
                header("X-Secret-Key", secretKey)
            }
            if (response.status.value == 200) response.body<List<String>>() else emptyList()
        } catch (_: Exception) {
            emptyList()
        }
    }

    suspend fun fetchStats(): LibraryStats? {
        return try {
            val response = client.get("$baseUrl/api/library/stats") {
                header("X-Secret-Key", secretKey)
            }
            if (response.status.value == 200) response.body<LibraryStats>() else null
        } catch (_: Exception) {
            null
        }
    }

    suspend fun fetchUsers(): List<UserInfo> {
        return try {
            val response = client.get("$baseUrl/api/users") {
                header("X-Secret-Key", secretKey)
            }
            if (response.status.value == 200) response.body<List<UserInfo>>() else emptyList()
        } catch (_: Exception) {
            emptyList()
        }
    }
}
