package com.max.reel.data

import com.max.reel.model.FileNode
import io.ktor.client.HttpClient
import io.ktor.client.call.body
import io.ktor.client.engine.android.Android
import io.ktor.client.plugins.contentnegotiation.ContentNegotiation
import io.ktor.client.request.get
import io.ktor.client.request.header
import io.ktor.client.request.post
import io.ktor.client.request.setBody
import io.ktor.http.ContentType
import io.ktor.http.contentType
import io.ktor.serialization.kotlinx.json.json
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

sealed interface LibraryResult {
    data class Success(val root: FileNode) : LibraryResult
    data object Unauthorized : LibraryResult
    data class NetworkError(val message: String) : LibraryResult
}

class ApiClient(private val prefs: PreferencesManager) {

    private val client = HttpClient(Android) {
        install(ContentNegotiation) {
            json(Json { ignoreUnknownKeys = true })
        }
        engine {
            connectTimeout = 5_000
            socketTimeout = 10_000
        }
        expectSuccess = false
    }

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
            ConnectResult.NetworkError(e.message ?: "Unknown error")
        }
    }

    suspend fun fetchLibrary(): LibraryResult {
        return try {
            val response = client.get("$baseUrl/api/library") {
                header("X-Secret-Key", prefs.secretKey)
            }
            when (response.status.value) {
                200 -> {
                    val root = response.body<FileNode>()
                    LibraryResult.Success(root)
                }
                401 -> LibraryResult.Unauthorized
                else -> LibraryResult.NetworkError("Unexpected status: ${response.status.value}")
            }
        } catch (e: Exception) {
            LibraryResult.NetworkError(e.message ?: "Unknown error")
        }
    }

    fun close() {
        client.close()
    }
}
