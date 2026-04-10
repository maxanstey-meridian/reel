package com.max.reel.ui.player

import com.max.reel.data.TrickplayMetadata
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

class ScrubStateTest {

    private fun metadata(intervalMs: Int = 5000, totalThumbs: Int = 100) = TrickplayMetadata(
        intervalMs = intervalMs,
        columns = 5,
        rows = 5,
        thumbWidth = 160,
        thumbHeight = 90,
        totalThumbs = totalThumbs,
        spritePath = "test",
    )

    @Test
    fun `enterScrub sets active true and stores position`() {
        val state = ScrubState()
        state.enterScrub(5000)
        assertTrue(state.active)
        assertEquals(5000L, state.positionMs)
    }

    @Test
    fun `moveScrub increments position`() {
        val state = ScrubState()
        state.metadata = metadata(intervalMs = 5000, totalThumbs = 100)
        state.enterScrub(0)
        state.moveScrub(5000)
        assertEquals(5000L, state.positionMs)
    }

    @Test
    fun `moveScrub clamps at zero`() {
        val state = ScrubState()
        state.metadata = metadata()
        state.enterScrub(1000)
        state.moveScrub(-5000)
        assertEquals(0L, state.positionMs)
    }

    @Test
    fun `moveScrub clamps at max`() {
        val state = ScrubState()
        state.metadata = metadata(intervalMs = 5000, totalThumbs = 10)
        state.enterScrub(40000)
        state.moveScrub(10000)
        assertEquals(45000L, state.positionMs)
    }

    @Test
    fun `commitScrub returns position and deactivates`() {
        val state = ScrubState()
        state.enterScrub(3000)
        val result = state.commitScrub()
        assertEquals(3000L, result)
        assertFalse(state.active)
    }

    @Test
    fun `cancelScrub deactivates without changing position`() {
        val state = ScrubState()
        state.enterScrub(3000)
        state.cancelScrub()
        assertFalse(state.active)
        assertEquals(3000L, state.positionMs)
    }

    @Test
    fun `moveScrub is no-op when metadata is null`() {
        val state = ScrubState()
        state.enterScrub(1000)
        state.moveScrub(5000)
        assertEquals(1000L, state.positionMs)
    }

    @Test
    fun `moveScrub accumulates multiple moves`() {
        val state = ScrubState()
        state.metadata = metadata(intervalMs = 5000, totalThumbs = 100)
        state.enterScrub(0)
        state.moveScrub(5000)
        state.moveScrub(5000)
        state.moveScrub(5000)
        assertEquals(15000L, state.positionMs)
    }

    @Test
    fun `max position is (totalThumbs - 1) times intervalMs`() {
        val state = ScrubState()
        state.metadata = metadata(intervalMs = 10000, totalThumbs = 5)
        state.enterScrub(0)
        state.moveScrub(Long.MAX_VALUE)
        assertEquals(40000L, state.positionMs)
    }
}
