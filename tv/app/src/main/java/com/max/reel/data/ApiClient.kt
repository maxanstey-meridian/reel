package com.max.reel.data

import io.ktor.client.HttpClient
import io.ktor.client.call.NoTransformationFoundException
import io.ktor.client.call.body
import io.ktor.client.engine.HttpClientEngine
import io.ktor.client.engine.android.Android
import io.ktor.client.plugins.HttpTimeout
import io.ktor.client.plugins.contentnegotiation.ContentNegotiation
import io.ktor.client.plugins.defaultRequest
import io.ktor.client.request.HttpRequestBuilder
import io.ktor.client.request.header
import io.ktor.client.request.parameter
import io.ktor.client.request.request
import io.ktor.client.request.setBody
import io.ktor.client.statement.HttpResponse
import io.ktor.http.ContentType
import io.ktor.http.HttpMethod
import io.ktor.http.URLBuilder
import io.ktor.http.URLProtocol
import io.ktor.http.appendPathSegments
import io.ktor.http.contentType
import io.ktor.serialization.ContentConvertException
import io.ktor.serialization.kotlinx.json.json
import kotlinx.coroutines.CancellationException
import kotlinx.serialization.json.Json

private const val KEY_HEADER = "X-Secret-Key"

class ApiClient(private val pairing: PairingStore, engine: HttpClientEngine = Android.create()) {
    private val client =
        HttpClient(engine) {
            install(HttpTimeout) { requestTimeoutMillis = 5_000 }
            install(ContentNegotiation) {
                json(
                    Json {
                        ignoreUnknownKeys = true
                        coerceInputValues = true
                    },
                )
            }
            defaultRequest { contentType(ContentType.Application.Json) }
            expectSuccess = false
        }

    suspend fun connect(server: DiscoveredServer): ApiResult<ConnectResponse> = request(pairedKey = null) {
        client.request {
            method = HttpMethod.Post
            url { at(server.host, server.port, "api/connect") }
            setBody(ConnectRequest(server.key))
        }
    }

    suspend fun nextEpisode(path: String): ApiResult<NextEpisodeResponse> = paired("api/library/next") {
        parameter("path", path)
    }

    suspend fun startPlayback(path: String): ApiResult<PlaybackSessionResponse> = paired("api/playback/session") {
        method = HttpMethod.Post
        setBody(StartPlaybackRequest(path))
    }

    suspend fun getPlaybackSession(): ApiResult<PlaybackSessionEnvelope> = paired("api/playback/session")

    suspend fun updatePlaybackIntent(body: UpdatePlaybackIntentRequest): ApiResult<PlaybackSessionResponse> =
        paired("api/playback/session/intent") {
            method = HttpMethod.Put
            setBody(body)
        }

    suspend fun reportObservation(body: ReportPlaybackObservationRequest): ApiResult<Unit> =
        paired("api/playback/session/observation") {
            method = HttpMethod.Put
            setBody(body)
        }

    suspend fun updateProgress(path: String, positionSeconds: Double, durationSeconds: Double): ApiResult<Unit> =
        paired("api/progress") {
            method = HttpMethod.Post
            setBody(ProgressRequest(path, positionSeconds, durationSeconds))
        }

    suspend fun fetchSubtitles(path: String): ApiResult<List<SubtitleTrack>> = paired("api/files/subtitles") {
        parameter("path", path)
    }

    suspend fun streamInfo(path: String): ApiResult<StreamInfo> = paired("api/files/stream/info") {
        parameter("path", path)
    }

    fun pairUrl(server: PairedServer): String = url(server, "pair", "key" to server.key)

    // With a height the stream is a transcode starting at `start` that cannot range-seek; without, the file itself.
    fun streamUrl(server: PairedServer, path: String, height: Int?, start: Double): String = if (height == null) {
        url(server, "api/files/stream", "path" to path)
    } else {
        url(
            server,
            "api/files/stream",
            "path" to path,
            "height" to "$height",
            "start" to "$start",
        )
    }

    fun subtitleUrl(server: PairedServer, path: String, index: Int, start: Double): String = url(
        server,
        "api/files/subtitles/content",
        "path" to path,
        "index" to "$index",
        "start" to "$start",
    )

    fun mediaHeaders(server: PairedServer): Map<String, String> = mapOf(KEY_HEADER to server.key)

    private fun url(server: PairedServer, route: String, vararg query: Pair<String, String>): String = URLBuilder()
        .apply {
            at(server.host, server.port, route)
            query.forEach { (name, value) -> parameters.append(name, value) }
        }.buildString()

    // IPv6 literals need brackets in a URL authority.
    private fun URLBuilder.at(host: String, port: Int, route: String) {
        protocol = URLProtocol.HTTP
        this.host = if (':' in host) "[$host]" else host
        this.port = port
        appendPathSegments(route)
    }

    private suspend inline fun <reified T> paired(
        route: String,
        crossinline configure: HttpRequestBuilder.() -> Unit = {},
    ): ApiResult<T> {
        val server = pairing.server.value ?: return ApiResult.Failure("Not paired")
        return request(pairedKey = server.key) {
            client.request {
                url { at(server.host, server.port, route) }
                header(KEY_HEADER, server.key)
                configure()
            }
        }
    }

    // Only 401 means the key is wrong, and it unpairs only if the TV still holds that key;
    // 403 is the server's private-network check and must not cost the pairing. A success that
    // can't be decoded keeps its status, so callers can tell it from a dropped connection.
    private suspend inline fun <reified T> request(
        pairedKey: String?,
        crossinline send: suspend () -> HttpResponse,
    ): ApiResult<T> = try {
        val response = send()
        val status = response.status.value
        if (status in 200..299) {
            val unexpected = ApiResult.Failure("Unexpected response from the server", status)
            try {
                ApiResult.Success(response.body())
            } catch (e: ContentConvertException) {
                unexpected
            } catch (e: NoTransformationFoundException) {
                unexpected
            }
        } else {
            if (status == 401 && pairedKey != null && pairing.server.value?.key == pairedKey) {
                pairing.forget()
            }
            val fallback =
                when (status) {
                    403 -> "The server only accepts devices on its own network"
                    else -> "Unexpected status: $status"
                }
            val body = runCatching { response.body<ErrorResponse>() }.getOrNull()
            ApiResult.Failure(body?.message ?: fallback, status)
        }
    } catch (e: CancellationException) {
        throw e
    } catch (e: Exception) {
        ApiResult.Failure(e.message ?: "Network error")
    }
}
