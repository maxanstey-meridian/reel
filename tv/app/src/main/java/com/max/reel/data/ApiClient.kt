package com.max.reel.data

import io.ktor.client.HttpClient
import io.ktor.client.call.body
import io.ktor.client.engine.android.Android
import io.ktor.client.plugins.contentnegotiation.ContentNegotiation
import io.ktor.client.request.get
import io.ktor.client.request.header
import io.ktor.client.request.post
import io.ktor.client.request.put
import io.ktor.client.request.setBody
import io.ktor.http.ContentType
import io.ktor.http.contentType
import io.ktor.http.encodeURLParameter
import io.ktor.serialization.kotlinx.json.json
import kotlinx.coroutines.CancellationException
import kotlinx.serialization.SerialName
import kotlinx.serialization.Serializable
import kotlinx.serialization.json.Json

@Serializable
private data class ConnectRequestBody(val key: String)

@Serializable
private data class ConnectResponseBody(
    val status: String,
    @SerialName("serverName") val serverName: String? = null,
)

sealed interface ConnectResult {
    data class Success(val serverName: String) : ConnectResult
    data object Unauthorized : ConnectResult
    data class NetworkError(val message: String) : ConnectResult
}

class ApiClient(
    private val prefs: PreferencesManager,
    private val client: HttpClient = HttpClient(Android) {
        install(ContentNegotiation) {
            json(Json { ignoreUnknownKeys = true })
        }
        engine {
            connectTimeout = 5_000
            socketTimeout = 10_000
        }
        expectSuccess = false
    },
) {

    private val baseUrl: String
        get() = "http://${prefs.serverIp}:${prefs.serverPort}"

    suspend fun connect(ip: String, port: String, key: String): ConnectResult {
        return try {
            val response = client.post("http://$ip:$port/api/connect") {
                contentType(ContentType.Application.Json)
                setBody(ConnectRequestBody(key))
            }
            when (response.status.value) {
                200 -> {
                    val body = response.body<ConnectResponseBody>()
                    ConnectResult.Success(body.serverName ?: "")
                }
                401 -> ConnectResult.Unauthorized
                else -> ConnectResult.NetworkError("Unexpected status: ${response.status.value}")
            }
        } catch (e: Exception) {
            if (e is CancellationException) throw e
            ConnectResult.NetworkError(e.message ?: "Unknown error")
        }
    }

    suspend fun fetchCastCurrent(): CastPollResult {
        return try {
            val response = client.get("$baseUrl/api/cast/current") {
                header("X-Secret-Key", prefs.secretKey)
            }
            when (response.status.value) {
                200 -> {
                    val status = response.body<CastStatus>()
                    CastPollResult.Success(status)
                }
                204 -> CastPollResult.Success(null)
                401 -> CastPollResult.Unauthorized
                else -> CastPollResult.NetworkError("Unexpected status: ${response.status.value}")
            }
        } catch (e: Exception) {
            if (e is CancellationException) throw e
            CastPollResult.NetworkError(e.message ?: "Unknown error")
        }
    }

    suspend fun acknowledgeCast(): Boolean {
        return try {
            val response = client.post("$baseUrl/api/cast/ack") {
                header("X-Secret-Key", prefs.secretKey)
            }
            response.status.value == 200
        } catch (e: Exception) {
            if (e is CancellationException) throw e
            false
        }
    }

    suspend fun fetchPosters(count: Int = 20): List<String> {
        return try {
            val response = client.get("$baseUrl/api/library/posters?count=$count") {
                header("X-Secret-Key", prefs.secretKey)
            }
            if (response.status.value == 200) response.body<List<String>>() else emptyList()
        } catch (e: Exception) {
            if (e is CancellationException) throw e
            emptyList()
        }
    }

    suspend fun fetchStats(): LibraryStats? {
        return try {
            val response = client.get("$baseUrl/api/library/stats") {
                header("X-Secret-Key", prefs.secretKey)
            }
            if (response.status.value == 200) response.body<LibraryStats>() else null
        } catch (e: Exception) {
            if (e is CancellationException) throw e
            null
        }
    }

    suspend fun fetchUsers(): List<UserInfo> {
        return try {
            val response = client.get("$baseUrl/api/users") {
                header("X-Secret-Key", prefs.secretKey)
            }
            if (response.status.value == 200) response.body<List<UserInfo>>() else emptyList()
        } catch (e: Exception) {
            if (e is CancellationException) throw e
            emptyList()
        }
    }

    suspend fun updateProgress(
        path: String,
        positionSeconds: Double,
        durationSeconds: Double,
    ): Boolean {
        return try {
            val response = client.post("$baseUrl/api/progress") {
                header("X-Secret-Key", prefs.secretKey)
                contentType(ContentType.Application.Json)
                setBody(ProgressRequestBody(path, positionSeconds, durationSeconds))
            }
            response.status.value == 200
        } catch (e: Exception) {
            if (e is CancellationException) throw e
            false
        }
    }

    suspend fun setWatched(path: String): Boolean {
        return try {
            val response = client.put("$baseUrl/api/media/watched?path=${path.encodeURLParameter()}") {
                header("X-Secret-Key", prefs.secretKey)
            }
            response.status.value == 204
        } catch (e: Exception) {
            if (e is CancellationException) throw e
            false
        }
    }

    fun close() {
        client.close()
    }
}

@Serializable
private data class ProgressRequestBody(
    val relativePath: String,
    val positionSeconds: Double,
    val durationSeconds: Double,
)
