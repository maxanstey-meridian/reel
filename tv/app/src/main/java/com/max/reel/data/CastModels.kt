package com.max.reel.data

import kotlinx.serialization.Serializable

@Serializable
data class CastCommand(
    val type: String,
    val relativePath: String? = null,
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

@Serializable
data class LibraryStats(val totalFiles: Int, val totalBytes: Long)

@Serializable
data class UserInfo(val id: Int, val name: String, val icon: String? = null)
