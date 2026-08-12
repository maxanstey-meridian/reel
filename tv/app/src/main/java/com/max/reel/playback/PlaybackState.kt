package com.max.reel.playback

import com.max.reel.data.PlaybackDesiredState
import com.max.reel.data.PlaybackObservationState
import com.max.reel.data.PlaybackSessionResponse
import com.max.reel.data.UpdatePlaybackIntentRequest
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.CompletableDeferred
import kotlinx.coroutines.Job
import kotlinx.coroutines.channels.Channel
import kotlinx.coroutines.cancelAndJoin
import kotlinx.coroutines.delay
import kotlinx.coroutines.launch
import kotlinx.coroutines.withTimeoutOrNull

data class PlaybackUiState(
    val connection: PlaybackConnection = PlaybackConnection.Stopped,
    val sessionId: String? = null,
    val relativePath: String? = null,
    val actualState: PlaybackObservationState = PlaybackObservationState.Idle,
    val positionSeconds: Double = 0.0,
    val durationSeconds: Double = 0.0,
    val subtitleIndex: Int? = null,
    val error: PlaybackError? = null,
    val controlPending: Boolean = false,
    val controlError: String? = null,
    val feedback: PlaybackFeedback? = null,
    val seekDestinationSeconds: Double? = null,
    val pairingInvalid: Boolean = false,
)

enum class PlaybackFeedback { Playing, Paused }

sealed interface PlaybackConnection {
    data object Stopped : PlaybackConnection
    data object Connected : PlaybackConnection
    data class Retrying(val attempt: Int, val retryInSeconds: Long, val message: String) : PlaybackConnection
}

sealed interface PlaybackError {
    val code: String
    val message: String

    data class CannotReachServer(override val message: String = "Can't reach server") : PlaybackError {
        override val code = "server_unreachable"
    }

    data object Unauthorized : PlaybackError {
        override val code = "unauthorized"
        override val message = "Invalid server key"
    }

    data object FileNotFound : PlaybackError {
        override val code = "file_not_found"
        override val message = "File not found"
    }

    data class Unknown(override val message: String) : PlaybackError {
        override val code = "playback_error"
    }
}

data class AppliedIntent(
    val sessionId: String? = null,
    val revision: Long = -1,
    val seekId: Long? = null,
    val relativePath: String? = null,
    val subtitleIndex: Int? = null,
)

data class ReconciliationDecision(
    val loadPath: String? = null,
    val desiredState: PlaybackDesiredState? = null,
    val subtitleIndex: Int? = null,
    val subtitleChanged: Boolean = false,
    val seekId: Long? = null,
    val seekPositionSeconds: Double? = null,
)

fun reconcile(session: PlaybackSessionResponse, applied: AppliedIntent): ReconciliationDecision {
    val isNewSession = session.sessionId != applied.sessionId
    val revisionAdvanced = isNewSession || session.revision > applied.revision
    if (!revisionAdvanced) return ReconciliationDecision()

    val seek = session.intent.pendingSeek?.takeIf {
        session.revision >= it.introducedRevision &&
            (isNewSession || applied.seekId == null || it.seekId > applied.seekId)
    }
    return ReconciliationDecision(
        loadPath = session.intent.relativePath.takeIf { isNewSession || it != applied.relativePath },
        desiredState = session.intent.desiredState,
        subtitleIndex = session.intent.subtitleIndex,
        subtitleChanged = isNewSession || session.intent.subtitleIndex != applied.subtitleIndex,
        seekId = seek?.seekId,
        seekPositionSeconds = seek?.positionSeconds,
    )
}

internal const val PROGRESS_INTERVAL_MS = 10_000L

fun isProgressDue(isPlaying: Boolean, nowMs: Long, lastReportMs: Long?): Boolean =
    isPlaying && (lastReportMs == null || nowMs - lastReportMs >= PROGRESS_INTERVAL_MS)

fun retryDelayMs(attempt: Int): Long = (1_000L shl (attempt - 1).coerceIn(0, 3))

fun pollingFailure(attempt: Int, message: String): PlaybackConnection.Retrying =
    PlaybackConnection.Retrying(attempt, retryDelayMs(attempt) / 1_000, message)

class PollingFailureTracker {
    var failures: Int = 0
        private set

    fun succeeded(): PlaybackConnection {
        failures = 0
        return PlaybackConnection.Connected
    }

    fun failed(message: String): PlaybackConnection.Retrying {
        failures++
        return pollingFailure(failures, message)
    }
}

enum class AppDestination { Pairing, Ready, Player }

fun appDestination(isPaired: Boolean, relativePath: String?): AppDestination = when {
    !isPaired -> AppDestination.Pairing
    relativePath != null -> AppDestination.Player
    else -> AppDestination.Ready
}

fun localPlayPauseIntent(isPlaying: Boolean): PlaybackDesiredState =
    if (isPlaying) PlaybackDesiredState.Paused else PlaybackDesiredState.Playing

fun lifecycleIntent(hasActiveSession: Boolean): PlaybackDesiredState? =
    PlaybackDesiredState.Paused.takeIf { hasActiveSession }

data class LifecycleDecision(
    val pollingEnabled: Boolean,
    val commandsEnabled: Boolean,
    val mediaSessionEnabled: Boolean,
    val pauseIntent: PlaybackDesiredState?,
)

fun lifecycleDecision(foreground: Boolean, hasActiveSession: Boolean) = LifecycleDecision(
    pollingEnabled = foreground,
    commandsEnabled = foreground,
    mediaSessionEnabled = foreground,
    pauseIntent = if (foreground) null else lifecycleIntent(hasActiveSession),
)

fun localIntentRequest(
    session: PlaybackSessionResponse,
    desiredState: PlaybackDesiredState,
): UpdatePlaybackIntentRequest = UpdatePlaybackIntentRequest(
    sessionId = session.sessionId,
    desiredState = desiredState,
    subtitleIndex = session.intent.subtitleIndex,
    seekPositionSeconds = null,
)

fun loadingState(state: PlaybackUiState, path: String, subtitleIndex: Int?): PlaybackUiState =
    state.copy(
        relativePath = path,
        subtitleIndex = subtitleIndex,
        actualState = PlaybackObservationState.Loading,
        positionSeconds = 0.0,
        durationSeconds = 0.0,
        error = null,
    )

class SessionApplicationGate {
    private var generation = 0L
    private var sessionId: String? = null
    private var revision = -1L

    fun requestGeneration(): Long = generation

    fun acceptPoll(requestGeneration: Long, session: PlaybackSessionResponse?): Boolean {
        if (requestGeneration != generation) return false
        if (session == null) {
            generation++
            sessionId = null
            revision = -1
            return true
        }
        if (session.sessionId == sessionId && session.revision < revision) return false
        sessionId = session.sessionId
        revision = session.revision
        return true
    }

    fun acceptCommand(session: PlaybackSessionResponse) {
        generation++
        sessionId = session.sessionId
        revision = session.revision
    }

    fun clear() {
        generation++
        sessionId = null
        revision = -1
    }
}

data class ProgressSnapshot(val sessionId: String?, val path: String, val position: Double, val duration: Double)

class ProgressWriter(
    scope: CoroutineScope,
    private val send: suspend (ProgressSnapshot) -> Boolean,
    private val onSuccess: (ProgressSnapshot) -> Unit = {},
) {
    private sealed interface Command {
        data class Write(val snapshot: ProgressSnapshot) : Command
        data class Flush(val snapshot: ProgressSnapshot?, val result: CompletableDeferred<Boolean>) : Command
        data class Close(val snapshot: ProgressSnapshot?, val result: CompletableDeferred<Boolean>) : Command
    }

    private val commands = Channel<Command>(Channel.UNLIMITED)
    private val job: Job = scope.launch {
        var pending: ProgressSnapshot? = null
        var nextCommand: Command? = null
        while (true) {
            val command = nextCommand ?: commands.receiveCatching().getOrNull() ?: break
            nextCommand = null
            when (command) {
                is Command.Write -> {
                    pending = command.snapshot
                    while (true) {
                        when (val next = commands.tryReceive().getOrNull()) {
                            is Command.Write -> pending = next.snapshot
                            null -> break
                            else -> {
                                nextCommand = next
                                break
                            }
                        }
                    }
                    pending?.let { deliver(it) }
                    pending = null
                }
                is Command.Flush -> {
                    command.snapshot?.let { pending = it }
                    pending?.let { deliver(it) }
                    pending = null
                    command.result.complete(true)
                }
                is Command.Close -> {
                    command.snapshot?.let { pending = it }
                    pending?.let { deliver(it) }
                    command.result.complete(true)
                    break
                }
            }
        }
    }

    private suspend fun deliver(snapshot: ProgressSnapshot) {
        while (!send(snapshot)) delay(1_000)
        onSuccess(snapshot)
    }

    fun request(snapshot: ProgressSnapshot) { commands.trySend(Command.Write(snapshot)) }

    suspend fun flush(snapshot: ProgressSnapshot? = null, timeoutMs: Long = 2_000): Boolean {
        val result = CompletableDeferred<Boolean>()
        if (!commands.trySend(Command.Flush(snapshot, result)).isSuccess) return false
        return withTimeoutOrNull(timeoutMs) { result.await() } ?: false
    }

    suspend fun close(snapshot: ProgressSnapshot? = null, timeoutMs: Long = 2_000): Boolean {
        val result = CompletableDeferred<Boolean>()
        if (!commands.trySend(Command.Close(snapshot, result)).isSuccess) return false
        val flushed = withTimeoutOrNull(timeoutMs) { result.await() } ?: false
        commands.close()
        if (flushed) job.join() else job.cancelAndJoin()
        return flushed
    }
}

fun nextObservationSequence(local: Long?, server: Long?): Long = maxOf(local ?: 0, server ?: 0) + 1

fun availableSubtitleIndex(requested: Int?, available: Collection<Int>): Int? =
    requested?.takeIf(available::contains)

class ObservationQueue<T>(
    scope: CoroutineScope,
    private val send: suspend (T) -> Boolean,
    private val onFailure: (T) -> Unit = {},
) {
    private val requests = Channel<T>(Channel.CONFLATED)

    init {
        scope.launch {
            var retry: T? = null
            while (true) {
                val next = retry ?: requests.receiveCatching().getOrNull() ?: break
                val latest = requests.tryReceive().getOrNull() ?: next
                if (send(latest)) retry = null else {
                    onFailure(latest)
                    retry = latest
                    delay(1_000)
                }
            }
        }
    }

    fun request(snapshot: T) { requests.trySend(snapshot) }

    fun close() {
        requests.close()
    }
}
