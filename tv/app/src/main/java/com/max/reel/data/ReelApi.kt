package com.max.reel.data

import kotlinx.serialization.SerialName
import kotlinx.serialization.Serializable

interface ReelApi {
    suspend fun getPlaybackSession(): ApiResult<PlaybackSessionEnvelope>
    suspend fun updatePlaybackIntent(request: UpdatePlaybackIntentRequest): ApiResult<PlaybackSessionResponse>
    suspend fun reportObservation(request: ReportPlaybackObservationRequest): ApiResult<PlaybackSessionResponse>
    suspend fun updateProgress(path: String, positionSeconds: Double, durationSeconds: Double): ApiResult<Unit>
    suspend fun fetchSubtitles(path: String): ApiResult<List<SubtitleTrack>>
    fun streamUrl(path: String): String
    fun subtitleContentUrl(path: String, index: Int, format: String = "webvtt"): String
}

sealed interface ApiResult<out T> {
    data class Success<T>(val value: T) : ApiResult<T>
    data class Failure(val message: String, val statusCode: Int? = null) : ApiResult<Nothing>
}

@Serializable
data class PlaybackSessionEnvelope(val session: PlaybackSessionResponse?)

@Serializable
data class PlaybackSessionResponse(
    val sessionId: String,
    val revision: Long,
    val createdAtUtc: String,
    val updatedAtUtc: String,
    val intent: PlaybackIntentResponse,
    val observation: PlaybackObservationResponse? = null,
    val tvConnected: Boolean,
)

@Serializable
data class PlaybackIntentResponse(
    val relativePath: String,
    val desiredState: PlaybackDesiredState,
    val subtitleIndex: Int? = null,
    val pendingSeek: SeekRequestResponse? = null,
)

@Serializable
data class SeekRequestResponse(
    val seekId: Long,
    val positionSeconds: Double,
    val introducedRevision: Long,
)

@Serializable
data class PlaybackObservationResponse(
    val observationSequence: Long,
    val appliedRevision: Long,
    val appliedSeekId: Long? = null,
    val state: PlaybackObservationState,
    val relativePath: String? = null,
    val positionSeconds: Double,
    val durationSeconds: Double,
    val subtitleIndex: Int? = null,
    val errorCode: String? = null,
    val errorMessage: String? = null,
    val updatedAtUtc: String,
)

@Serializable
enum class PlaybackDesiredState {
    @SerialName("playing") Playing,
    @SerialName("paused") Paused,
    @SerialName("stopped") Stopped,
}

@Serializable
enum class PlaybackObservationState {
    @SerialName("idle") Idle,
    @SerialName("loading") Loading,
    @SerialName("buffering") Buffering,
    @SerialName("playing") Playing,
    @SerialName("paused") Paused,
    @SerialName("ended") Ended,
    @SerialName("error") Error,
}

@Serializable
data class ReportPlaybackObservationRequest(
    val sessionId: String,
    val observationSequence: Long,
    val appliedRevision: Long,
    val appliedSeekId: Long? = null,
    val state: PlaybackObservationState,
    val relativePath: String? = null,
    val positionSeconds: Double,
    val durationSeconds: Double,
    val subtitleIndex: Int? = null,
    val errorCode: String? = null,
    val errorMessage: String? = null,
)

@Serializable
data class UpdatePlaybackIntentRequest(
    val sessionId: String,
    val desiredState: PlaybackDesiredState,
    val subtitleIndex: Int? = null,
    val seekPositionSeconds: Double? = null,
)
