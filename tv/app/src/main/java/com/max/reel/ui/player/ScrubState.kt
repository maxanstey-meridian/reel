package com.max.reel.ui.player

import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.setValue
import com.max.reel.data.TrickplayMetadata

class ScrubState {
    var active: Boolean by mutableStateOf(false)
        private set
    var positionMs: Long by mutableStateOf(0L)
        private set
    var metadata: TrickplayMetadata? by mutableStateOf(null)

    fun enterScrub(currentPositionMs: Long) {
        active = true
        positionMs = currentPositionMs
    }

    fun moveScrub(deltaMs: Long) {
        val meta = metadata ?: return
        val max = (meta.totalThumbs - 1).toLong() * meta.intervalMs.toLong()
        positionMs = (positionMs + deltaMs).coerceIn(0L, max)
    }

    fun commitScrub(): Long {
        active = false
        return positionMs
    }

    fun cancelScrub() {
        active = false
    }
}
