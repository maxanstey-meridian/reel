package com.max.reel.playback

import com.max.reel.data.PlaybackDesiredState
import com.max.reel.data.PlaybackIntentResponse
import com.max.reel.data.PlaybackSessionResponse
import com.max.reel.data.SeekRequestResponse
import com.max.reel.data.PlaybackObservationState
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test
import kotlinx.coroutines.CompletableDeferred
import kotlinx.coroutines.test.advanceUntilIdle
import kotlinx.coroutines.test.runCurrent
import kotlinx.coroutines.test.runTest

class PlaybackStateTest {
    private fun session(revision: Long, seekId: Long? = null) = PlaybackSessionResponse(
        sessionId = "session-1",
        revision = revision,
        createdAtUtc = "2026-08-11T10:00:00Z",
        updatedAtUtc = "2026-08-11T10:00:01Z",
        intent = PlaybackIntentResponse(
            relativePath = "Films/movie.mp4",
            desiredState = PlaybackDesiredState.Playing,
            pendingSeek = seekId?.let { SeekRequestResponse(it, 42.0, revision) },
        ),
        tvConnected = true,
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
    fun `seek is acknowledged only at its introducing revision`() {
        val futureSeek = session(4, 9).copy(
            intent = session(4, 9).intent.copy(
                pendingSeek = SeekRequestResponse(9, 42.0, introducedRevision = 5),
            ),
        )

        assertNull(reconcile(futureSeek, AppliedIntent("session-1", revision = 3)).seekId)
        assertEquals(9L, reconcile(futureSeek.copy(revision = 5), AppliedIntent("session-1", revision = 4)).seekId)
    }

    @Test
    fun `subtitle disabling is represented as an explicit change`() {
        val decision = reconcile(
            session(4),
            AppliedIntent("session-1", revision = 3, subtitleIndex = 2),
        )

        assertTrue(decision.subtitleChanged)
        assertNull(decision.subtitleIndex)
    }

    @Test
    fun `progress is due only each ten seconds while playing`() {
        assertTrue(isProgressDue(true, 10_000, null))
        assertTrue(isProgressDue(true, 20_000, 10_000))
        assertEquals(false, isProgressDue(true, 19_999, 10_000))
        assertEquals(false, isProgressDue(false, 30_000, 10_000))
    }

    @Test
    fun `poll failure exposes bounded retry without changing playback state`() {
        val tracker = PollingFailureTracker()
        assertEquals(PlaybackConnection.Retrying(1, 1, "offline"), tracker.failed("offline"))
        repeat(6) { tracker.failed("offline") }
        assertEquals(PlaybackConnection.Retrying(8, 8, "offline"), tracker.failed("offline"))
        assertEquals(PlaybackConnection.Connected, tracker.succeeded())
        assertEquals(0, tracker.failures)
    }

    @Test
    fun `pairing remains authoritative while playback chooses ready or player`() {
        assertEquals(AppDestination.Pairing, appDestination(false, "Films/movie.mp4"))
        assertEquals(AppDestination.Ready, appDestination(true, null))
        assertEquals(AppDestination.Player, appDestination(true, "Films/movie.mp4"))
    }

    @Test
    fun `accepted local stop does not restart on the next identical poll`() {
        val stopped = session(4).copy(
            intent = session(4).intent.copy(desiredState = PlaybackDesiredState.Stopped),
        )
        val applied = AppliedIntent(
            sessionId = stopped.sessionId,
            revision = stopped.revision,
            relativePath = stopped.intent.relativePath,
        )

        assertEquals(ReconciliationDecision(), reconcile(stopped, applied))
    }

    @Test
    fun `hardware play pause preserves subtitle and sends no seek`() {
        val current = session(3).copy(intent = session(3).intent.copy(subtitleIndex = 2))
        val request = localIntentRequest(current, localPlayPauseIntent(isPlaying = true))

        assertEquals(PlaybackDesiredState.Paused, request.desiredState)
        assertEquals(2, request.subtitleIndex)
        assertNull(request.seekPositionSeconds)
    }

    @Test
    fun `loading snapshot carries the new path before observation`() {
        val loading = loadingState(
            PlaybackUiState(relativePath = "old.mp4", positionSeconds = 90.0, durationSeconds = 120.0),
            "new.mp4",
            1,
        )

        assertEquals("new.mp4", loading.relativePath)
        assertEquals(1, loading.subtitleIndex)
        assertEquals(PlaybackObservationState.Loading, loading.actualState)
        assertEquals(0.0, loading.positionSeconds, 0.0)
        assertEquals(0.0, loading.durationSeconds, 0.0)
    }

    @Test
    fun `background lifecycle pauses only an active session`() {
        assertEquals(PlaybackDesiredState.Paused, lifecycleIntent(hasActiveSession = true))
        assertNull(lifecycleIntent(hasActiveSession = false))
    }

    @Test
    fun `background lifecycle synchronously disables all control surfaces`() {
        assertEquals(
            LifecycleDecision(false, false, false, PlaybackDesiredState.Paused),
            lifecycleDecision(foreground = false, hasActiveSession = true),
        )
        assertEquals(
            LifecycleDecision(true, true, true, null),
            lifecycleDecision(foreground = true, hasActiveSession = true),
        )
    }

    @Test
    fun `delayed poll cannot overwrite command or resurrect old session`() {
        val gate = SessionApplicationGate()
        val delayedPoll = gate.requestGeneration()
        gate.acceptCommand(session(4))

        assertEquals(false, gate.acceptPoll(delayedPoll, session(3)))
        val oldSessionPoll = gate.requestGeneration()
        gate.acceptCommand(session(5).copy(sessionId = "session-2"))
        assertEquals(false, gate.acceptPoll(oldSessionPoll, session(9)))

        val endingPoll = gate.requestGeneration()
        assertTrue(gate.acceptPoll(endingPoll, null))
        assertEquals(false, gate.acceptPoll(endingPoll, session(9)))
    }

    @Test
    fun `observations are sent serially in transition order`() = runTest {
        val firstMayFinish = CompletableDeferred<Unit>()
        val sent = mutableListOf<String>()
        val queue = ObservationQueue<String>(
            scope = this,
            send = {
                if (it == "loading") firstMayFinish.await()
                sent += it
                true
            },
        )

        queue.request("loading")
        runCurrent()
        queue.request("playing")
        firstMayFinish.complete(Unit)
        advanceUntilIdle()

        assertEquals(listOf("loading", "playing"), sent)
        queue.close()
    }

    @Test
    fun `progress retries failure and supersedes it with latest position`() = runTest {
        val attempts = mutableListOf<Double>()
        val writer = ProgressWriter(
            scope = this,
            send = {
                attempts += it.position
                attempts.size > 1
            },
        )

        writer.request(ProgressSnapshot("session-1", "movie.mp4", 10.0, 60.0))
        runCurrent()
        writer.request(ProgressSnapshot("session-1", "movie.mp4", 20.0, 60.0))
        assertTrue(writer.close())

        assertEquals(listOf(10.0, 10.0, 20.0), attempts)
    }

    @Test
    fun `progress flush is a delivery barrier and close keeps worker as sole writer`() = runTest {
        val sent = mutableListOf<Double>()
        val writer = ProgressWriter(this, send = { sent += it.position; true })

        writer.request(ProgressSnapshot("session-1", "old.mp4", 10.0, 60.0))
        assertTrue(writer.flush(ProgressSnapshot("session-1", "old.mp4", 20.0, 60.0)))
        assertTrue(writer.close(ProgressSnapshot("session-1", "old.mp4", 30.0, 60.0)))

        assertEquals(listOf(10.0, 20.0, 30.0), sent)
    }

    @Test
    fun `observation sequence resumes above local or server maximum`() {
        assertEquals(8L, nextObservationSequence(local = 3, server = 7))
        assertEquals(9L, nextObservationSequence(local = 8, server = 4))
    }

    @Test
    fun `missing requested subtitle is disabled instead of substituted`() {
        assertEquals(2, availableSubtitleIndex(2, listOf(1, 2, 3)))
        assertNull(availableSubtitleIndex(4, listOf(1, 2, 3)))
        assertNull(availableSubtitleIndex(null, listOf(1, 2, 3)))
    }
}
