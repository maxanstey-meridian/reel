package com.max.reel.playback

import androidx.media3.common.C
import com.max.reel.data.StreamInfo
import com.max.reel.data.StreamMode
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test

class StreamTimelineTest {
    @Test
    fun `a direct stream is the file, so its clock is the player's`() {
        val timeline = StreamTimeline.of(
            StreamInfo(durationSeconds = null, mode = StreamMode.Direct),
            startSeconds = 300.0,
        )

        assertFalse(timeline.live)
        assertEquals(0.0, timeline.offsetSeconds, 0.0)
        assertEquals(312.5, timeline.positionSeconds(playerPositionMs = 312_500), 0.0)
        assertEquals(1_440.0, timeline.durationSeconds(playerDurationMs = 1_440_000), 0.0)
    }

    @Test
    fun `a live stream starts at the requested position and takes the file's duration`() {
        for (info in listOf(
            StreamInfo(durationSeconds = 1_440.0, mode = StreamMode.Transcode, transcodeHeight = 720),
            StreamInfo(durationSeconds = 1_440.0, mode = StreamMode.Remux),
        )) {
            val timeline = StreamTimeline.of(info, startSeconds = 300.0)

            assertTrue(timeline.live)
            assertEquals(312.5, timeline.positionSeconds(playerPositionMs = 12_500), 0.0)
            assertEquals(1_440.0, timeline.durationSeconds(playerDurationMs = 15_000), 0.0)
        }
    }

    @Test
    fun `an embedded subtitle maps to its container track only when both list the same number`() {
        val embedded = listOf(2, 3, 5)

        assertEquals(1, containerTrackPosition(embedded, index = 3, containerTextTracks = 3))
        assertNull(containerTrackPosition(embedded, index = 4, containerTextTracks = 3))
        // Media3 skipped a track ffprobe listed, or added captions of its own.
        assertNull(containerTrackPosition(embedded, index = 3, containerTextTracks = 2))
        assertNull(containerTrackPosition(embedded, index = 3, containerTextTracks = 4))
    }

    @Test
    fun `unknown or unset player values never produce negative or bogus times`() {
        val timeline =
            StreamTimeline.of(
                StreamInfo(durationSeconds = null, mode = StreamMode.Transcode, transcodeHeight = 720),
                startSeconds = -5.0,
            )

        assertEquals(0.0, timeline.positionSeconds(playerPositionMs = C.TIME_UNSET), 0.0)
        assertEquals(0.0, timeline.durationSeconds(playerDurationMs = C.TIME_UNSET), 0.0)
        assertEquals(
            0.0,
            StreamTimeline
                .of(
                    info = null,
                    startSeconds = 30.0,
                ).durationSeconds(playerDurationMs = 0),
            0.0,
        )
    }
}
