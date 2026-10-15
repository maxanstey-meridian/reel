package com.max.reel.playback

import android.content.Context
import android.os.SystemClock
import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import androidx.media3.common.Player
import com.max.reel.data.ApiClient
import com.max.reel.data.ApiResult
import com.max.reel.data.DiscoveredServer
import com.max.reel.data.PairingStore
import com.max.reel.data.PlaybackDesiredState
import com.max.reel.data.PlaybackObservationState
import com.max.reel.data.PlaybackObservationState.Buffering
import com.max.reel.data.PlaybackObservationState.Ended
import com.max.reel.data.PlaybackObservationState.Idle
import com.max.reel.data.PlaybackObservationState.Loading
import com.max.reel.data.PlaybackObservationState.Paused
import com.max.reel.data.PlaybackObservationState.Playing
import com.max.reel.data.PlaybackSessionResponse
import com.max.reel.data.ReportPlaybackObservationRequest
import com.max.reel.playback.PlaybackConnection.Connected
import com.max.reel.playback.PlaybackConnection.Disconnected
import kotlinx.coroutines.CancellationException
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.Job
import kotlinx.coroutines.SupervisorJob
import kotlinx.coroutines.async
import kotlinx.coroutines.cancel
import kotlinx.coroutines.coroutineScope
import kotlinx.coroutines.currentCoroutineContext
import kotlinx.coroutines.delay
import kotlinx.coroutines.ensureActive
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.collectLatest
import kotlinx.coroutines.flow.distinctUntilChanged
import kotlinx.coroutines.flow.filterNotNull
import kotlinx.coroutines.flow.firstOrNull
import kotlinx.coroutines.flow.map
import kotlinx.coroutines.flow.update
import kotlinx.coroutines.job
import kotlinx.coroutines.launch
import kotlinx.coroutines.sync.Mutex
import kotlinx.coroutines.sync.withLock
import kotlinx.coroutines.withTimeoutOrNull

class PlaybackViewModel(
    context: Context,
    private val pairing: PairingStore,
    private val apiClient: ApiClient,
    private val discover: () -> Flow<DiscoveredServer>,
) : ViewModel() {
    private val stream =
        StreamPlayer(context, apiClient, onSubtitleUnavailable = {
            _state.update {
                it.copy(subtitleIndex = null, notice = SUBTITLE_NOT_SHOWABLE)
            }
            reportObservation()
        })
    val player: Player get() = stream.player

    private val _state = MutableStateFlow(PlaybackUiState())
    val state: StateFlow<PlaybackUiState> = _state.asStateFlow()

    // Serialises every session read-and-apply (polls and TV commands) so a poll that was in
    // flight when a command landed can never apply an older session over it.
    private val commandMutex = Mutex()
    private var activeSession: PlaybackSessionResponse? = null
    private var applied = AppliedIntent()
    private var needsReapply = false
    private var loadJob: Job? = null

    // Set while the activity is stopped; nothing new is loaded until polling starts again.
    private var backgrounded = false

    // The session whose load already failed once on the network, so the next failure plays without info.
    private var unreachableLoadSessionId: String? = null

    // One skip request is sent at a time, since a transcoded seek restarts the stream; presses
    // meanwhile only move the target, which follows as a single request.
    private var skipJob: Job? = null
    private var skipTargetSeconds = 0.0
    private var unsentSkipSeconds: Double? = null

    private var lastReportedState = Idle
    private var observationSequence = 0L
    private val observations = MutableStateFlow<ReportPlaybackObservationRequest?>(null)
    private var nextEpisodeJob: Job? = null
    private var lastProgressMs = 0L
    private var progressJob: Job? = null
    private val saveScope = CoroutineScope(SupervisorJob() + Dispatchers.Main.immediate)
    private val progressWriter =
        ProgressWriter { snapshot ->
            val result = apiClient.updateProgress(snapshot.path, snapshot.position, snapshot.duration)
            when (result) {
                is ApiResult.Success -> true
                is ApiResult.Failure -> result.statusCode == 404
            }
        }

    init {
        player.addListener(
            object : Player.Listener {
                override fun onEvents(player: Player, events: Player.Events) = updateActualState()
            },
        )
        viewModelScope.launch {
            observations.filterNotNull().collect { observation ->
                val result = apiClient.reportObservation(observation)
                if (result is ApiResult.Failure && result.statusCode == null) {
                    _state.update { it.copy(connection = Disconnected) }
                }
            }
        }
    }

    // Runs while the app is started; polling follows pairing, and losing it stops playback.
    suspend fun poll() {
        backgrounded = false
        pairing.server.map { it != null }.distinctUntilChanged().collectLatest { paired ->
            if (paired) pollLoop() else stopCurrentPlayback()
        }
    }

    fun togglePlayPause() {
        if (_state.value.controlPending) return
        val desired =
            if (player.playWhenReady) PlaybackDesiredState.Paused else PlaybackDesiredState.Playing
        viewModelScope.launch { submitLocalIntent(desired) }
    }

    // Back must work even when the server is unreachable: stop here first, report afterwards.
    fun stopFromTv() {
        stopLocally()
        if (activeSession == null) return
        viewModelScope.launch {
            submitLocalIntent(PlaybackDesiredState.Stopped, authoritative = true)
        }
    }

    // Presses add up while a skip is in flight; any other pending control drops them.
    fun skip(seconds: Double) {
        val sending = skipJob?.isActive == true
        if (_state.value.controlPending && !sending) return
        val from = if (sending) skipTargetSeconds else stream.positionSeconds
        skipTargetSeconds = skipTarget(from, seconds, stream.durationSeconds)
        unsentSkipSeconds = skipTargetSeconds
        if (!sending) skipJob = viewModelScope.launch { sendSkips() }
    }

    // Stopping or backgrounding clears the unsent target; each request keeps the session's
    // play/pause intent as it is when sent.
    private suspend fun sendSkips() {
        while (true) {
            commandMutex.withLock {
                val target = unsentSkipSeconds ?: return
                unsentSkipSeconds = null
                val session = activeSession ?: return
                sendLocalIntent(session, session.intent.desiredState, seekPositionSeconds = target)
            }
        }
    }

    fun playNext() {
        val path = _state.value.nextEpisode?.relativePath ?: return
        if (_state.value.controlPending) return
        _state.update { it.copy(controlPending = true, notice = null) }
        viewModelScope.launch {
            commandMutex.withLock {
                val result = apiClient.startPlayback(path)
                if (result is ApiResult.Success) applySession(result.value)
                _state.update {
                    it.copy(
                        controlPending = false,
                        notice = (result as? ApiResult.Failure)?.let(::controlFailed) ?: it.notice,
                    )
                }
            }
        }
    }

    fun pauseForBackground() {
        backgrounded = true
        unsentSkipSeconds = null
        loadJob?.cancel()
        player.pause()
        reportProgress()
        if (_state.value.relativePath == null) return
        viewModelScope.launch {
            val accepted = submitLocalIntent(PlaybackDesiredState.Paused, authoritative = true)
            // An older command applied while we waited for the mutex may have resumed playback.
            player.pause()
            if (!accepted) needsReapply = true
        }
    }

    private suspend fun pollLoop() {
        var failures = 0
        var unreachable = 0
        while (true) {
            val result =
                commandMutex.withLock {
                    apiClient.getPlaybackSession().also {
                        if (it is ApiResult.Success) applyPolledSession(it.value.session)
                    }
                }
            val failure = result as? ApiResult.Failure
            failures = if (failure == null) 0 else failures + 1
            // After an outage a load gets its retry again rather than playing without info at once.
            if (failure != null) unreachableLoadSessionId = null
            // A success that can't be decoded is something else answering on the old address.
            val lost = failure != null && (failure.statusCode == null || failure.statusCode in 200..299)
            unreachable = if (lost) unreachable + 1 else 0
            val connection = if (failure == null) Connected else Disconnected
            val refusal = failure?.takeIf { it.statusCode != null }?.message
            _state.update { it.copy(connection = connection, connectionError = refusal) }
            if (failure == null) {
                val now = SystemClock.elapsedRealtime()
                if (player.isPlaying && now - lastProgressMs >= 10_000) reportProgress()
                saveProgress()
            }
            if (unreachable == 3) {
                unreachable = 0
                if (rediscover()) continue
            }
            delay(1_000L shl (failures - 1).coerceIn(0, 3))
        }
    }

    // The server's address can change under DHCP; its key does not, so it is found again by key.
    private suspend fun rediscover(): Boolean {
        val saved = pairing.server.value ?: return false
        val found =
            withTimeoutOrNull(10_000) { discover().firstOrNull { it.key == saved.key } }
                ?: return false
        if (found.host == saved.host && found.port == saved.port) return false
        pairing.save(saved.copy(host = found.host, port = found.port))
        return true
    }

    private suspend fun applyPolledSession(session: PlaybackSessionResponse?) {
        if (session == null) {
            if (activeSession != null) stopCurrentPlayback()
            return
        }
        applySession(session)
    }

    // Every applied session is reported afterwards, even an unchanged one: that report is the TV's
    // heartbeat, and it must carry the revision and seek just applied.
    private suspend fun applySession(session: PlaybackSessionResponse) {
        val decision = reconcile(session, applied, reapply = needsReapply)
        if (session.sessionId != activeSession?.sessionId) observationSequence = 0
        activeSession = session
        if (decision == ReconciliationDecision() || applyDecision(session, decision)) needsReapply = false
        reportObservation()
    }

    // `applied` only records what reached the player, so an interrupted or deferred load is decided
    // again later; returns whether it did. A player stopped by an error stays stopped until it is
    // prepared again. A backgrounded app opens no stream: the next poll after it starts loads it.
    private suspend fun applyDecision(session: PlaybackSessionResponse, decision: ReconciliationDecision): Boolean {
        val seek = decision.seekPositionSeconds
        if (decision.desiredState != PlaybackDesiredState.Stopped) {
            val rebuild =
                decision.loadPath != null || decision.subtitleChanged || stream.error != null ||
                    (seek != null && stream.timeline.transcoded)
            val start = seek ?: if (decision.loadPath != null) 0.0 else stream.positionSeconds
            if (rebuild && (backgrounded || !loadMedia(session, start))) return false
            if (!rebuild && seek != null) stream.seekTo(seek)
            if (seek != null) _state.update { it.copy(seekDestinationSeconds = seek) }
        }

        applied =
            AppliedIntent(
                sessionId = session.sessionId,
                revision = session.revision,
                seekId =
                decision.seekId ?: applied.seekId.takeIf { applied.sessionId == session.sessionId },
                relativePath = session.intent.relativePath,
                subtitleIndex = session.intent.subtitleIndex,
            )
        when (decision.desiredState) {
            PlaybackDesiredState.Playing -> {
                player.play()
            }

            PlaybackDesiredState.Paused -> {
                player.pause()
                reportProgress()
            }

            PlaybackDesiredState.Stopped -> {
                stopLocally()
            }

            null -> {
                Unit
            }
        }
        return true
    }

    // Clearing applied.relativePath makes any newer revision of this session reload the media.
    private fun stopLocally() {
        unsentSkipSeconds = null
        loadJob?.cancel()
        reportProgress()
        player.stop()
        applied = applied.copy(relativePath = null)
        _state.update {
            it.copy(
                relativePath = null,
                title = null,
                actualState = Idle,
                subtitleIndex = null,
                error = null,
                seekDestinationSeconds = null,
                nextEpisode = null,
            )
        }
        reportObservation()
    }

    private fun stopCurrentPlayback() {
        activeSession = null
        applied = AppliedIntent()
        stopLocally()
    }

    private suspend fun submitLocalIntent(desiredState: PlaybackDesiredState, authoritative: Boolean = false): Boolean =
        commandMutex.withLock {
            val session = activeSession ?: return@withLock false
            sendLocalIntent(session, desiredState, authoritative = authoritative)
        }

    // Callers hold commandMutex. Key presses send expectedRevision so they cannot silently revert a
    // newer phone change; authoritative intents (Back, backgrounding) must win regardless.
    private suspend fun sendLocalIntent(
        session: PlaybackSessionResponse,
        desiredState: PlaybackDesiredState,
        seekPositionSeconds: Double? = null,
        authoritative: Boolean = false,
    ): Boolean {
        val request =
            localIntentRequest(session, desiredState, seekPositionSeconds)
                .let { if (authoritative) it.copy(expectedRevision = null) else it }
        _state.update { it.copy(controlPending = true, notice = null) }
        val result = apiClient.updatePlaybackIntent(request)
        if (result is ApiResult.Success) applySession(result.value)
        val conflict = (result as? ApiResult.Failure)?.statusCode == 409
        if (conflict) {
            (apiClient.getPlaybackSession() as? ApiResult.Success)?.let {
                applyPolledSession(it.value.session)
            }
        }
        _state.update {
            it.copy(
                controlPending = false,
                notice =
                (result as? ApiResult.Failure)?.takeUnless { conflict }?.let(::controlFailed) ?: it.notice,
            )
        }
        return result is ApiResult.Success
    }

    // Fetches before touching the player, so a stop or background during the fetch leaves it as it was.
    // A file without info or subtitles plays as it is. A network failure (e.g. a slow probe timing out)
    // is tried again on the next poll once; failing again, the file plays without them.
    private suspend fun loadMedia(session: PlaybackSessionResponse, startSeconds: Double): Boolean {
        val intent = session.intent
        val path = intent.relativePath
        val (infoResult, tracksResult) =
            try {
                coroutineScope {
                    loadJob = coroutineContext.job
                    val info = async { apiClient.streamInfo(path) }
                    val tracks = async { apiClient.fetchSubtitles(path) }
                    info.await() to tracks.await()
                }
            } catch (e: CancellationException) {
                currentCoroutineContext().ensureActive()
                return false
            }
        val unreachable = listOf(infoResult, tracksResult).any { it is ApiResult.Failure && it.statusCode == null }
        if (unreachable && unreachableLoadSessionId != session.sessionId) {
            unreachableLoadSessionId = session.sessionId
            return false
        }
        unreachableLoadSessionId = null
        val server = pairing.server.value ?: return false
        val info = (infoResult as? ApiResult.Success)?.value
        val tracks = (tracksResult as? ApiResult.Success)?.value.orEmpty()

        reportProgress()
        val track = tracks.firstOrNull { it.index == intent.subtitleIndex }
        _state.update {
            it.copy(
                relativePath = path,
                title = intent.title,
                subtitleIndex = track?.index,
                actualState = Loading,
                error = null,
                nextEpisode = null,
                notice =
                when {
                    intent.subtitleIndex == null || track != null -> null
                    tracksResult is ApiResult.Failure -> SUBTITLES_NOT_LOADED
                    else -> SUBTITLES_UNAVAILABLE
                },
            )
        }
        stream.load(server, path, info, tracks, track, startSeconds)
        return true
    }

    private fun updateActualState() {
        val error = stream.error
        val actual =
            when {
                error != null -> PlaybackObservationState.Error
                player.playbackState == Player.STATE_READY -> if (player.isPlaying) Playing else Paused
                player.playbackState == Player.STATE_ENDED -> Ended
                _state.value.actualState == Loading -> Loading
                player.playbackState == Player.STATE_BUFFERING -> Buffering
                else -> Idle
            }
        _state.update { it.copy(actualState = actual, error = error) }
        if (actual == lastReportedState) return
        lastReportedState = actual
        nextEpisodeJob?.cancel()
        _state.update { it.copy(nextEpisode = null) }
        val path = _state.value.relativePath
        if (actual == Ended && path != null) {
            nextEpisodeJob =
                viewModelScope.launch {
                    val next = (apiClient.nextEpisode(path) as? ApiResult.Success)?.value
                    _state.update { it.copy(nextEpisode = next) }
                }
        }
        reportObservation()
        if (actual == Paused || actual == Ended) reportProgress()
    }

    // Nothing is reported for a session until something of it has been applied.
    private fun reportObservation() {
        val session = activeSession?.takeIf { it.sessionId == applied.sessionId } ?: return
        val snapshot = _state.value
        observationSequence =
            maxOf(observationSequence, session.observation?.observationSequence ?: 0) + 1
        observations.value =
            ReportPlaybackObservationRequest(
                sessionId = session.sessionId,
                observationSequence = observationSequence,
                appliedRevision = applied.revision,
                appliedSeekId = applied.seekId,
                state = snapshot.actualState,
                relativePath = snapshot.relativePath,
                positionSeconds = stream.positionSeconds,
                durationSeconds = stream.durationSeconds,
                subtitleIndex = snapshot.subtitleIndex,
                errorCode = snapshot.error?.code,
                errorMessage = snapshot.error?.message,
            )
    }

    private fun saveProgress() {
        if (progressJob?.isActive != true) progressJob = saveScope.launch { progressWriter.save() }
    }

    private fun reportProgress() {
        captureProgress()?.let {
            progressWriter.request(it)
            lastProgressMs = SystemClock.elapsedRealtime()
            saveProgress()
        }
    }

    private fun captureProgress(): ProgressSnapshot? {
        val path = _state.value.relativePath ?: return null
        val duration = stream.durationSeconds.takeIf { it > 0 } ?: return null
        val ended = _state.value.actualState == Ended
        val position = if (ended) duration else stream.positionSeconds.coerceAtMost(duration)
        return ProgressSnapshot(path, position, duration)
    }

    override fun onCleared() {
        captureProgress()?.let(progressWriter::request)
        player.release()
        saveScope.launch {
            withTimeoutOrNull(2_000) {
                progressJob?.join()
                progressWriter.save()
            }
            saveScope.cancel()
        }
    }
}

private const val SUBTITLES_UNAVAILABLE = "Subtitles aren't available for this file"
private const val SUBTITLES_NOT_LOADED = "Couldn't load subtitles"
private const val SUBTITLE_NOT_SHOWABLE = "This subtitle track can't be shown"

private fun controlFailed(failure: ApiResult.Failure) = "Control failed: ${failure.message}"
