package com.max.reel.data

import kotlinx.serialization.SerialName
import kotlinx.serialization.Serializable

sealed interface ApiResult<out T> {
    data class Success<T>(val value: T) : ApiResult<T>

    data class Failure(val message: String, val statusCode: Int? = null) : ApiResult<Nothing>
}

@Serializable
data class ConnectRequest(val key: String)

@Serializable
data class ConnectResponse(val serverName: String? = null)

@Serializable
data class PlaybackSessionEnvelope(val session: PlaybackSessionResponse?)

@Serializable
data class PlaybackSessionResponse(
    val sessionId: String,
    val revision: Long,
    val intent: PlaybackIntentResponse,
    val observation: PlaybackObservationResponse? = null,
)

@Serializable
data class PlaybackIntentResponse(
    val relativePath: String,
    val title: String,
    val desiredState: PlaybackDesiredState,
    val subtitleIndex: Int? = null,
    val pendingSeek: SeekRequestResponse? = null,
)

@Serializable
data class SeekRequestResponse(val seekId: Long, val positionSeconds: Double)

@Serializable
data class PlaybackObservationResponse(val observationSequence: Long)

@Serializable
enum class PlaybackDesiredState {
    @SerialName("playing")
    Playing,

    @SerialName("paused")
    Paused,

    @SerialName("stopped")
    Stopped,
}

@Serializable
enum class PlaybackObservationState {
    @SerialName("idle")
    Idle,

    @SerialName("loading")
    Loading,

    @SerialName("buffering")
    Buffering,

    @SerialName("playing")
    Playing,

    @SerialName("paused")
    Paused,

    @SerialName("ended")
    Ended,

    @SerialName("error")
    Error,
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
    val expectedRevision: Long? = null,
)

@Serializable
data class NextEpisodeResponse(val relativePath: String? = null, val title: String? = null)

@Serializable
data class StartPlaybackRequest(val relativePath: String)

@Serializable
data class ProgressRequest(val relativePath: String, val positionSeconds: Double, val durationSeconds: Double)

@Serializable
// durationSeconds is null when the server can't tell.
data class StreamInfo(val durationSeconds: Double? = null, val transcodeHeight: Int? = null)

@Serializable
data class SubtitleTrack(val index: Int, val source: String, val imageBased: Boolean)

@Serializable
data class ErrorResponse(val code: String, val message: String)
