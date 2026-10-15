package com.max.reel.playback

import androidx.media3.common.C
import com.max.reel.data.StreamInfo
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

class StreamTimelineTest {
    @Test
    fun `a direct stream is the file, so its clock is the player's`() {
        val timeline = StreamTimeline.of(StreamInfo(durationSeconds = 0.0), startSeconds = 300.0)

        assertFalse(timeline.transcoded)
        assertEquals(0.0, timeline.offsetSeconds, 0.0)
        assertEquals(312.5, timeline.positionSeconds(playerPositionMs = 312_500), 0.0)
        assertEquals(1_440.0, timeline.durationSeconds(playerDurationMs = 1_440_000), 0.0)
    }

    @Test
    fun `a transcoded stream starts at the requested position and takes the file's duration`() {
        val timeline =
            StreamTimeline.of(
                StreamInfo(durationSeconds = 1_440.0, transcodeHeight = 720),
                startSeconds = 300.0,
            )

        assertTrue(timeline.transcoded)
        assertEquals(312.5, timeline.positionSeconds(playerPositionMs = 12_500), 0.0)
        assertEquals(1_440.0, timeline.durationSeconds(playerDurationMs = 15_000), 0.0)
    }

    @Test
    fun `unknown or unset player values never produce negative or bogus times`() {
        val timeline =
            StreamTimeline.of(
                StreamInfo(durationSeconds = 0.0, transcodeHeight = 720),
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
