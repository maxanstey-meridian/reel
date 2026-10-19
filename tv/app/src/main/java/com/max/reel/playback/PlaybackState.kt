package com.max.reel.playback

import com.max.reel.data.NextEpisodeResponse
import com.max.reel.data.PlaybackDesiredState
import com.max.reel.data.PlaybackObservationState
import com.max.reel.data.PlaybackSessionResponse
import com.max.reel.data.UpdatePlaybackIntentRequest

data class PlaybackUiState(
    val connection: PlaybackConnection = PlaybackConnection.Connecting,
    // Set when the server answered but refused, e.g. this TV is outside its private network.
    val connectionError: String? = null,
    val relativePath: String? = null,
    val title: String? = null,
    val actualState: PlaybackObservationState = PlaybackObservationState.Idle,
    val subtitleIndex: Int? = null,
    val error: PlaybackError? = null,
    val controlPending: Boolean = false,
    // Shown briefly over the player: a failed control or a subtitle that can't be shown.
    val notice: String? = null,
    val seekDestinationSeconds: Double? = null,
    val nextEpisode: NextEpisodeResponse? = null,
)

// Connecting until the first poll answers.
enum class PlaybackConnection { Connecting, Connected, Disconnected }

data class PlaybackError(val code: String, val message: String)

data class AppliedIntent(
    val sessionId: String? = null,
    val revision: Long = -1,
    val seekId: Long? = null,
    val relativePath: String? = null,
    val subtitleIndex: Int? = null,
    val maxHeight: Int? = null,
)

data class ReconciliationDecision(
    val loadPath: String? = null,
    val desiredState: PlaybackDesiredState? = null,
    // Within a session (a new one always has a loadPath): a new quality reloads the stream at the
    // current position, and a new subtitle on its own is switched in place when the player already has it.
    val qualityChanged: Boolean = false,
    val subtitleChanged: Boolean = false,
    val seekId: Long? = null,
    val seekPositionSeconds: Double? = null,
)

// reapply re-asserts the current intent after the player diverged locally (e.g. a background
// pause the server never accepted) without moving appliedRevision backwards.
fun reconcile(
    session: PlaybackSessionResponse,
    applied: AppliedIntent,
    reapply: Boolean = false,
): ReconciliationDecision {
    val isNewSession = session.sessionId != applied.sessionId
    val revisionAdvanced = isNewSession || reapply || session.revision > applied.revision
    if (!revisionAdvanced) return ReconciliationDecision()

    val seek =
        session.intent.pendingSeek?.takeIf {
            isNewSession || applied.seekId == null || it.seekId > applied.seekId
        }
    return ReconciliationDecision(
        loadPath =
        session.intent.relativePath.takeIf {
            isNewSession || it != applied.relativePath
        },
        desiredState = session.intent.desiredState,
        qualityChanged = !isNewSession && session.intent.maxHeight != applied.maxHeight,
        subtitleChanged = !isNewSession && session.intent.subtitleIndex != applied.subtitleIndex,
        seekId = seek?.seekId,
        seekPositionSeconds = seek?.positionSeconds,
    )
}

fun localIntentRequest(
    session: PlaybackSessionResponse,
    desiredState: PlaybackDesiredState,
    seekPositionSeconds: Double? = null,
): UpdatePlaybackIntentRequest = UpdatePlaybackIntentRequest(
    sessionId = session.sessionId,
    desiredState = desiredState,
    subtitleIndex = session.intent.subtitleIndex,
    maxHeight = session.intent.maxHeight,
    seekPositionSeconds = seekPositionSeconds,
    expectedRevision = session.revision,
)

fun skipTarget(positionSeconds: Double, offsetSeconds: Double, durationSeconds: Double): Double {
    val target = (positionSeconds + offsetSeconds).coerceAtLeast(0.0)
    return if (durationSeconds > 0) target.coerceAtMost(durationSeconds) else target
}

data class ProgressSnapshot(val path: String, val position: Double, val duration: Double)

// Keeps only the current and previous file's latest unsaved position. One save() runs at a time
// (the caller owns scheduling); it also sends positions requested while it runs. A failed save
// never waits or retries here.
class ProgressWriter(private val send: suspend (ProgressSnapshot) -> Boolean) {
    private val pending = linkedMapOf<String, ProgressSnapshot>()

    fun request(snapshot: ProgressSnapshot) {
        pending.remove(snapshot.path)
        pending[snapshot.path] = snapshot
        while (pending.size > 2) pending.remove(pending.keys.first())
    }

    suspend fun save() {
        val attempted = mutableSetOf<ProgressSnapshot>()
        while (true) {
            val snapshot = pending.values.firstOrNull { it !in attempted } ?: return
            attempted += snapshot
            if (send(snapshot) && pending[snapshot.path] == snapshot) pending.remove(snapshot.path)
        }
    }
}
