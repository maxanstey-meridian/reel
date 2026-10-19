package com.max.reel.playback

import com.max.reel.data.PlaybackDesiredState
import com.max.reel.data.PlaybackIntentResponse
import com.max.reel.data.PlaybackSessionResponse
import com.max.reel.data.SeekRequestResponse
import kotlinx.coroutines.CompletableDeferred
import kotlinx.coroutines.launch
import kotlinx.coroutines.test.runCurrent
import kotlinx.coroutines.test.runTest
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test

class PlaybackStateTest {
    private fun session(revision: Long, seekId: Long? = null) = PlaybackSessionResponse(
        sessionId = "session-1",
        revision = revision,
        intent =
        PlaybackIntentResponse(
            relativePath = "Films/movie.mp4",
            title = "Movie",
            desiredState = PlaybackDesiredState.Playing,
            pendingSeek = seekId?.let { SeekRequestResponse(it, 42.0) },
        ),
    )

    @Test
    fun `stable intent is ignored until revision advances`() {
        val decision = reconcile(session(3), AppliedIntent("session-1", revision = 3))
        assertEquals(ReconciliationDecision(), decision)
    }

    @Test
    fun `only a newer seek id is applied`() {
        val stale = reconcile(session(4, 8), AppliedIntent("session-1", revision = 3, seekId = 8))
        val newer = reconcile(session(5, 9), AppliedIntent("session-1", revision = 4, seekId = 8))
        assertNull(stale.seekPositionSeconds)
        assertEquals(42.0, newer.seekPositionSeconds)
        assertEquals(9L, newer.seekId)
    }

    @Test
    fun `a subtitle change on its own is switched in place, not reloaded`() {
        val decision =
            reconcile(
                session(4),
                AppliedIntent("session-1", revision = 3, relativePath = "Films/movie.mp4", subtitleIndex = 2),
            )

        assertTrue(decision.subtitleChanged)
        assertEquals(false, decision.streamChanged)
        assertNull(decision.loadPath)
    }

    @Test
    fun `a new session loads its stream rather than switching subtitles`() {
        val decision = reconcile(session(1), AppliedIntent("session-0", revision = 5, subtitleIndex = 2))

        assertTrue(decision.streamChanged)
        assertEquals(false, decision.subtitleChanged)
    }

    @Test
    fun `a new quality reloads the stream`() {
        val current = session(4).copy(intent = session(4).intent.copy(maxHeight = 480))
        val applied = AppliedIntent("session-1", revision = 3, relativePath = "Films/movie.mp4")

        val decision = reconcile(current, applied)

        assertTrue(decision.streamChanged)
        assertNull(decision.loadPath)
    }

    @Test
    fun `accepted local stop does not restart on the next identical poll`() {
        val stopped =
            session(4).copy(
                intent = session(4).intent.copy(desiredState = PlaybackDesiredState.Stopped),
            )
        val applied =
            AppliedIntent(
                sessionId = stopped.sessionId,
                revision = stopped.revision,
                relativePath = stopped.intent.relativePath,
            )

        assertEquals(ReconciliationDecision(), reconcile(stopped, applied))
    }

    @Test
    fun `local intent preserves subtitle and quality and expects the revision it was based on`() {
        val current = session(3).copy(intent = session(3).intent.copy(subtitleIndex = 2, maxHeight = 720))
        val request = localIntentRequest(current, PlaybackDesiredState.Paused)

        assertEquals(PlaybackDesiredState.Paused, request.desiredState)
        assertEquals(2, request.subtitleIndex)
        assertEquals(720, request.maxHeight)
        assertNull(request.seekPositionSeconds)
        assertEquals(3L, request.expectedRevision)
    }

    @Test
    fun `reapply re-asserts desired state at the same revision without reloading`() {
        val applied = AppliedIntent("session-1", revision = 3, relativePath = "Films/movie.mp4")

        assertEquals(ReconciliationDecision(), reconcile(session(3), applied))
        val decision = reconcile(session(3), applied, reapply = true)
        assertEquals(PlaybackDesiredState.Playing, decision.desiredState)
        assertNull(decision.loadPath)
        assertEquals(false, decision.streamChanged)
    }

    @Test
    fun `locally stopped media reloads when a newer revision arrives`() {
        val applied = AppliedIntent("session-1", revision = 3, relativePath = null)

        assertEquals(ReconciliationDecision(), reconcile(session(3), applied))
        assertEquals("Films/movie.mp4", reconcile(session(4), applied).loadPath)
    }

    @Test
    fun `skip clamps to duration only when duration is known`() {
        assertEquals(40.0, skipTarget(30.0, 10.0, durationSeconds = 0.0), 0.0)
        assertEquals(60.0, skipTarget(55.0, 10.0, durationSeconds = 60.0), 0.0)
        assertEquals(0.0, skipTarget(5.0, -10.0, durationSeconds = 0.0), 0.0)
    }

    @Test
    fun `failed progress is replaced by latest position and does not block another file`() = runTest {
        val attempts = mutableListOf<Pair<String, Double>>()
        var reachable = false
        val writer =
            ProgressWriter {
                attempts += it.path to it.position
                reachable
            }
        writer.request(ProgressSnapshot("old.mp4", 30.0, 60.0))
        writer.save()
        writer.request(ProgressSnapshot("old.mp4", 10.0, 60.0))
        writer.request(ProgressSnapshot("new.mp4", 5.0, 60.0))
        reachable = true
        writer.save()
        writer.save()
        assertEquals(listOf("old.mp4" to 30.0, "old.mp4" to 10.0, "new.mp4" to 5.0), attempts)
    }

    @Test
    fun `position requested during an in-flight save is sent by that save`() = runTest {
        val release = CompletableDeferred<Unit>()
        val sent = mutableListOf<Double>()
        val writer =
            ProgressWriter {
                if (it.position == 30.0) release.await()
                sent += it.position
                true
            }
        writer.request(ProgressSnapshot("movie.mp4", 30.0, 60.0))
        val save = launch { writer.save() }
        runCurrent()
        writer.request(ProgressSnapshot("movie.mp4", 10.0, 60.0))
        release.complete(Unit)
        save.join()

        assertEquals(listOf(30.0, 10.0), sent)
    }
}
