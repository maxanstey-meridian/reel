package com.max.reel.ui.player

import org.junit.Assert.assertEquals
import org.junit.Test

class FormatTimestampTest {
    @Test
    fun `formats seconds only`() {
        assertEquals("0:05", formatTimestamp(5_000))
    }

    @Test
    fun `formats minutes and seconds`() {
        assertEquals("2:30", formatTimestamp(150_000))
    }

    @Test
    fun `formats hours minutes seconds`() {
        assertEquals("1:30:00", formatTimestamp(5_400_000))
    }

    @Test
    fun `formats zero`() {
        assertEquals("0:00", formatTimestamp(0))
    }

    @Test
    fun `pads seconds`() {
        assertEquals("1:05", formatTimestamp(65_000))
    }
}
