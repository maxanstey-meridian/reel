package com.max.reel.data

import kotlinx.serialization.Serializable

@Serializable
data class SubtitleTrack(
    val index: Int,
    val label: String,
    val language: String,
    val format: String,
    val source: String,
    val externalPath: String? = null,
)
