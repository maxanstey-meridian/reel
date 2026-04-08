package com.max.reel.data

import kotlinx.serialization.SerialName
import kotlinx.serialization.Serializable

@Serializable
enum class CastCommandType {
    Unknown,
    @SerialName("play") Play,
    @SerialName("stop") Stop,
}

@Serializable
data class CastCommand(
    val type: CastCommandType,
    val relativePath: String? = null,
    val positionSeconds: Double? = null,
)

@Serializable
data class CastStatus(
    val command: CastCommand? = null,
)

sealed interface CastPollResult {
    data class Success(val status: CastStatus?) : CastPollResult
    data object Unauthorized : CastPollResult
    data class NetworkError(val message: String) : CastPollResult
}

data class PlayCommand(val relativePath: String, val positionSeconds: Double? = null)

@Serializable
data class LibraryStats(val totalFiles: Int, val totalBytes: Long)

@Serializable
data class UserInfo(val id: Int, val name: String, val icon: String? = null)
