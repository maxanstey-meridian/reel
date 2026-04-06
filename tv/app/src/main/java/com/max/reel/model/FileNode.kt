package com.max.reel.model

import kotlinx.serialization.SerialName
import kotlinx.serialization.Serializable

@Serializable
enum class NodeType {
    @SerialName("file") FILE,
    @SerialName("directory") DIRECTORY,
}

@Serializable
data class FileNode(
    val name: String,
    val type: NodeType,
    @SerialName("relativePath") val relativePath: String,
    val extension: String? = null,
    @SerialName("sizeBytes") val sizeBytes: Long? = null,
    val children: List<FileNode>? = null,
)
