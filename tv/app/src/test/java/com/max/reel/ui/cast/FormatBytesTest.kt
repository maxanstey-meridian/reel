package com.max.reel.ui.cast

import org.junit.Assert.assertEquals
import org.junit.Test

class FormatBytesTest {

    @Test
    fun `zero bytes`() {
        assertEquals("0 B", formatBytes(0))
    }

    @Test
    fun `small bytes below KB`() {
        assertEquals("512 B", formatBytes(512))
    }

    @Test
    fun `kilobytes`() {
        assertEquals("512 KB", formatBytes(512L * 1024))
    }

    @Test
    fun `megabytes`() {
        assertEquals("100 MB", formatBytes(100L * 1024 * 1024))
    }

    @Test
    fun `gigabytes with decimal`() {
        // 42.7 GB = 42.7 * 1024^3
        val bytes = (42.7 * 1024 * 1024 * 1024).toLong()
        assertEquals("42.7 GB", formatBytes(bytes))
    }

    @Test
    fun `exactly 1 GB`() {
        assertEquals("1.0 GB", formatBytes(1L shl 30))
    }

    @Test
    fun `terabytes`() {
        val bytes = (2.1 * 1024 * 1024 * 1024 * 1024).toLong()
        assertEquals("2.1 TB", formatBytes(bytes))
    }

    @Test
    fun `exactly 1 TB`() {
        assertEquals("1.0 TB", formatBytes(1L shl 40))
    }

    @Test
    fun `boundary at KB`() {
        assertEquals("1 KB", formatBytes(1024))
    }

    @Test
    fun `boundary at MB`() {
        assertEquals("1 MB", formatBytes(1L shl 20))
    }
}
