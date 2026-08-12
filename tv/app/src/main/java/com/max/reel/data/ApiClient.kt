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
import io.ktor.client.statement.HttpResponse
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
            json(Json { ignoreUnknownKeys = true; coerceInputValues = true })
        }
        engine {
            connectTimeout = 5_000
            socketTimeout = 10_000
        }
        expectSuccess = false
    },
) : ReelApi {

    private val baseUrl: String
        get() = "http://${prefs.serverIp}:${prefs.serverPort}"

    suspend fun connect(ip: String, port: String, key: String): ConnectResult {
        return try {
            val response = client.post("http://$ip:$port/api/connect") {
                contentType(ContentType.Application.Json)
                setBody(ConnectRequestBody(key))
            }
            when (response.status.value) {
                in 200..299 -> {
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

    override suspend fun getPlaybackSession(): ApiResult<PlaybackSessionEnvelope> = request {
        client.get("$baseUrl/api/playback/session") { authenticate() }
    }

    override suspend fun updatePlaybackIntent(
        request: UpdatePlaybackIntentRequest,
    ): ApiResult<PlaybackSessionResponse> = request {
        client.put("$baseUrl/api/playback/session/intent") {
            authenticate()
            contentType(ContentType.Application.Json)
            setBody(request)
        }
    }

    override suspend fun reportObservation(
        request: ReportPlaybackObservationRequest,
    ): ApiResult<PlaybackSessionResponse> = request {
        client.put("$baseUrl/api/playback/session/observation") {
            authenticate()
            contentType(ContentType.Application.Json)
            setBody(request)
        }
    }

    override suspend fun updateProgress(
        path: String,
        positionSeconds: Double,
        durationSeconds: Double,
    ): ApiResult<Unit> {
        return try {
            val response = client.post("$baseUrl/api/progress") {
                authenticate()
                contentType(ContentType.Application.Json)
                setBody(ProgressRequestBody(path, positionSeconds, durationSeconds))
            }
            if (response.status.value in 200..299) ApiResult.Success(Unit)
            else ApiResult.Failure("Unexpected status: ${response.status.value}", response.status.value)
        } catch (e: Exception) {
            if (e is CancellationException) throw e
            ApiResult.Failure(e.message ?: "Network error")
        }
    }

    override suspend fun fetchSubtitles(path: String): ApiResult<List<SubtitleTrack>> {
        return try {
            val response = client.get("$baseUrl/api/files/subtitles?path=${path.encodeURLParameter()}") {
                authenticate()
            }
            if (response.status.value == 200) ApiResult.Success(response.body())
            else ApiResult.Failure("Unexpected status: ${response.status.value}", response.status.value)
        } catch (e: Exception) {
            if (e is CancellationException) throw e
            ApiResult.Failure(e.message ?: "Network error")
        }
    }

    override fun streamUrl(path: String): String =
        "$baseUrl/api/files/stream?path=${path.encodeURLParameter()}"

    override fun subtitleContentUrl(path: String, index: Int, format: String): String {
        return "$baseUrl/api/files/subtitles/content?path=${path.encodeURLParameter()}&index=$index&format=$format"
    }

    fun close() {
        client.close()
    }

    private fun io.ktor.client.request.HttpRequestBuilder.authenticate() {
        header("X-Secret-Key", prefs.secretKey)
    }

    private suspend inline fun <reified T> request(
        crossinline block: suspend () -> HttpResponse,
    ): ApiResult<T> = try {
        val response = block()
        if (response.status.value !in 200..299) {
            val message = runCatching { response.body<PlaybackErrorResponse>().error }
                .getOrElse { "Unexpected status: ${response.status.value}" }
            ApiResult.Failure(message, response.status.value)
        } else {
            ApiResult.Success(response.body())
        }
    } catch (e: Exception) {
        if (e is CancellationException) throw e
        ApiResult.Failure(e.message ?: "Network error")
    }
}

@Serializable
private data class PlaybackErrorResponse(val error: String)

@Serializable
private data class ProgressRequestBody(
    val relativePath: String,
    val positionSeconds: Double,
    val durationSeconds: Double,
)
