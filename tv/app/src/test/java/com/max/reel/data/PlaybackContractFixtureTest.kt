package com.max.reel.data

import kotlinx.serialization.json.Json
import org.junit.Assert.assertEquals
import org.junit.Test

// The fixture is written by the server's contract test, so renaming a field the TV reads fails here.
class PlaybackContractFixtureTest {
    private val json = Json { ignoreUnknownKeys = true }

    @Test
    fun `server playback session fixture decodes`() {
        val text = checkNotNull(javaClass.getResource("/contract/playback-session.json")).readText()

        val session = checkNotNull(json.decodeFromString<PlaybackSessionEnvelope>(text).session)

        assertEquals(4L, session.revision)
        assertEquals("Series S01E02", session.intent.title)
        assertEquals(PlaybackDesiredState.Paused, session.intent.desiredState)
        assertEquals(1, session.intent.subtitleIndex)
        assertEquals(480, session.intent.maxHeight)
        assertEquals(612.5, session.intent.pendingSeek!!.positionSeconds, 0.0)
        assertEquals(9L, session.observation!!.observationSequence)
    }
}
