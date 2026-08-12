package com.max.reel.playback

import android.app.Application
import android.net.Uri
import android.os.SystemClock
import androidx.annotation.OptIn
import androidx.lifecycle.AndroidViewModel
import androidx.lifecycle.viewModelScope
import androidx.media3.common.C
import androidx.media3.common.MediaItem
import androidx.media3.common.MimeTypes
import androidx.media3.common.PlaybackException
import androidx.media3.common.Player
import androidx.media3.common.TrackSelectionOverride
import androidx.media3.common.util.UnstableApi
import androidx.media3.datasource.DefaultHttpDataSource
import androidx.media3.datasource.HttpDataSource
import androidx.media3.exoplayer.ExoPlayer
import androidx.media3.exoplayer.source.DefaultMediaSourceFactory
import com.max.reel.data.ApiClient
import com.max.reel.data.ApiResult
import com.max.reel.data.PlaybackDesiredState
import com.max.reel.data.PlaybackObservationState
import com.max.reel.data.PlaybackSessionResponse
import com.max.reel.data.PreferencesManager
import com.max.reel.data.ReelApi
import com.max.reel.data.ReportPlaybackObservationRequest
import kotlinx.coroutines.Job
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.delay
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.launch
import kotlinx.coroutines.sync.Mutex
import kotlinx.coroutines.sync.withLock

@OptIn(UnstableApi::class)
class PlaybackViewModel @JvmOverloads constructor(
    application: Application,
    injectedApi: ReelApi? = null,
) : AndroidViewModel(application) {
    val preferences = PreferencesManager(application)
    val apiClient = ApiClient(preferences)
    private val api: ReelApi = injectedApi ?: apiClient

    private val dataSourceFactory = DefaultHttpDataSource.Factory()
        .setDefaultRequestProperties(mapOf("X-Secret-Key" to preferences.secretKey))
    val player: ExoPlayer = ExoPlayer.Builder(application)
        .setMediaSourceFactory(DefaultMediaSourceFactory(dataSourceFactory))
        .build()

    private val _state = MutableStateFlow(PlaybackUiState())
    val state: StateFlow<PlaybackUiState> = _state.asStateFlow()

    private var pollingJob: Job? = null
    private var backgroundPauseJob: Job? = null
    private var activeSession: PlaybackSessionResponse? = null
    private var applied = AppliedIntent()
    private var lastProgressMs: Long? = null
    private var lastKnownDurationSeconds = 0.0
    private var lastReportedState = PlaybackObservationState.Idle
    private var foreground = false
    private var commandsEnabled = false
    private var desiredSubtitleIndex: Int? = null
    private val sessionGate = SessionApplicationGate()
    private val observationSequences = mutableMapOf<String, Long>()
    private val commandMutex = Mutex()
    private val observationQueue = ObservationQueue<ReportPlaybackObservationRequest>(
        scope = viewModelScope,
        send = { request ->
            when (val result = api.reportObservation(request)) {
                is ApiResult.Success -> true
                is ApiResult.Failure -> {
                    if (result.statusCode == 401 || result.statusCode == 403) invalidatePairing()
                    if (result.statusCode == 404 || result.statusCode == 409) {
                        refreshSession()
                        reportObservation()
                        true
                    } else false
                }
            }
        },
        onFailure = { _state.value = _state.value.copy(connection = pollingFailure(1, "Observation not delivered")) },
    )
    private val progressWriter = ProgressWriter(
        scope = CoroutineScope(viewModelScope.coroutineContext + Dispatchers.IO),
        send = { snapshot ->
            when (val result = api.updateProgress(snapshot.path, snapshot.position, snapshot.duration)) {
                is ApiResult.Success -> true
                is ApiResult.Failure -> {
                    if (result.statusCode == 401 || result.statusCode == 403) invalidatePairing()
                    false
                }
            }
        },
        onSuccess = { lastProgressMs = SystemClock.elapsedRealtime() },
    )

    init {
        player.addListener(object : Player.Listener {
            override fun onPlaybackStateChanged(playbackState: Int) = updateActualState()
            override fun onIsPlayingChanged(isPlaying: Boolean) = updateActualState()
            override fun onTracksChanged(tracks: androidx.media3.common.Tracks) = applySubtitleSelection(tracks)

            override fun onPlayerError(error: PlaybackException) {
                val classified = classifyError(error)
                updatePlayerSnapshot(PlaybackObservationState.Error, classified)
                reportObservation()
            }

            override fun onPositionDiscontinuity(
                oldPosition: Player.PositionInfo,
                newPosition: Player.PositionInfo,
                reason: Int,
            ) {
                if (reason == Player.DISCONTINUITY_REASON_SEEK) reportProgress()
            }
        })
    }

    fun setPollingEnabled(enabled: Boolean) {
        if (!enabled || !foreground) {
            pollingJob?.cancel()
            pollingJob = null
            _state.value = _state.value.copy(connection = PlaybackConnection.Stopped)
            return
        }
        if (pollingJob?.isActive == true) return
        pollingJob = viewModelScope.launch { pollLoop() }
    }

    fun forgetPairing() {
        reportProgress()
        stopCurrentPlayback()
        setPollingEnabled(false)
        preferences.clearPairing()
    }

    fun togglePlayPause() {
        if (!commandsEnabled || _state.value.controlPending) return
        val desired = localPlayPauseIntent(player.isPlaying)
        viewModelScope.launch { submitLocalIntent(desired) }
    }

    fun stopFromTv() {
        if (!commandsEnabled || _state.value.controlPending) return
        viewModelScope.launch { submitLocalIntent(PlaybackDesiredState.Stopped) }
    }

    fun pauseForBackground() {
        val decision = lifecycleDecision(foreground = false, hasActiveSession = _state.value.relativePath != null)
        foreground = false
        commandsEnabled = decision.commandsEnabled
        setPollingEnabled(decision.pollingEnabled)
        player.pause()
        backgroundPauseJob = viewModelScope.launch {
            val desired = decision.pauseIntent
            if (desired != null) {
                player.pause()
                reportProgress()
                val accepted = submitLocalIntent(desired) {
                    // Re-assert after any older in-flight command completes.
                    player.pause()
                    reportProgress()
                }
                if (!accepted) {
                    player.pause()
                    applied = applied.copy(revision = -1)
                }
            } else if (player.isPlaying) {
                player.pause()
                applied = applied.copy(revision = -1)
            }
            reportProgress()
        }
    }

    fun resumeAfterBackground(paired: Boolean) {
        val decision = lifecycleDecision(foreground = true, hasActiveSession = _state.value.relativePath != null)
        foreground = true
        commandsEnabled = decision.commandsEnabled
        viewModelScope.launch {
            backgroundPauseJob?.join()
            if (paired) setPollingEnabled(true)
        }
    }

    fun onPaired() {
        _state.value = _state.value.copy(pairingInvalid = false)
        foreground = true
        commandsEnabled = true
        setPollingEnabled(true)
    }

    private suspend fun pollLoop() {
        val failures = PollingFailureTracker()
        while (true) {
            val requestGeneration = sessionGate.requestGeneration()
            when (val result = api.getPlaybackSession()) {
                is ApiResult.Success -> {
                    if (!foreground || !sessionGate.acceptPoll(requestGeneration, result.value.session)) continue
                    _state.value = _state.value.copy(connection = failures.succeeded())
                    val session = result.value.session
                    if (session == null) {
                        if (activeSession != null) stopCurrentPlayback()
                    } else {
                        applySession(session)
                        reportObservation()
                    }
                    maybeReportProgress()
                    delay(1_000)
                }
                is ApiResult.Failure -> {
                    if (result.statusCode == 401 || result.statusCode == 403) {
                        invalidatePairing()
                        return
                    }
                    val connection = failures.failed(result.message)
                    val retryMs = connection.retryInSeconds * 1_000
                    _state.value = _state.value.copy(connection = connection)
                    delay(retryMs)
                }
            }
        }
    }

    private suspend fun applySession(session: PlaybackSessionResponse) {
        val decision = reconcile(session, applied)
        observationSequences[session.sessionId] = maxOf(
            observationSequences[session.sessionId] ?: 0,
            session.observation?.observationSequence ?: 0,
        )
        if (decision.loadPath != null) {
            val oldProgress = captureProgress()
            if (oldProgress != null && !progressWriter.flush(oldProgress)) {
                _state.value = _state.value.copy(controlError = "Previous playback progress was not saved")
                return
            }
        }
        activeSession = session
        _state.value = _state.value.copy(sessionId = session.sessionId)
        if (decision == ReconciliationDecision()) return

        applied = AppliedIntent(
            sessionId = session.sessionId,
            revision = session.revision,
            seekId = decision.seekId ?: applied.seekId,
            relativePath = session.intent.relativePath,
            subtitleIndex = session.intent.subtitleIndex,
        )

        if (decision.desiredState == PlaybackDesiredState.Stopped) {
            reportProgress()
            player.stop()
            activeSession = session
            _state.value = _state.value.copy(relativePath = null, actualState = PlaybackObservationState.Idle)
            reportObservation()
            return
        }

        val needsRebuild = decision.loadPath != null || decision.subtitleChanged
        if (needsRebuild) {
            loadMedia(
                path = session.intent.relativePath,
                subtitleIndex = session.intent.subtitleIndex,
                startPositionSeconds = decision.seekPositionSeconds
                    ?: if (decision.loadPath != null) 0.0 else player.currentPosition / 1_000.0,
            )
        } else if (decision.seekPositionSeconds != null) {
            player.seekTo((decision.seekPositionSeconds * 1_000).toLong())
            showSeekOverlay(decision.seekPositionSeconds)
            reportProgress()
        }

        when (decision.desiredState) {
            PlaybackDesiredState.Playing -> player.play()
            PlaybackDesiredState.Paused -> {
                player.pause()
                reportProgress()
            }
            PlaybackDesiredState.Stopped, null -> Unit
        }
    }

    private suspend fun submitLocalIntent(
        desiredState: PlaybackDesiredState,
        beforeRequest: (() -> Unit)? = null,
    ): Boolean {
        return commandMutex.withLock {
            val session = activeSession ?: return@withLock false
            beforeRequest?.invoke()
            _state.value = _state.value.copy(controlPending = true, controlError = null)
            when (val result = api.updatePlaybackIntent(localIntentRequest(session, desiredState))) {
                is ApiResult.Success -> {
                    sessionGate.acceptCommand(result.value)
                    applySession(result.value)
                    _state.value = _state.value.copy(controlPending = false, controlError = null)
                    true
                }
                is ApiResult.Failure -> {
                    if (result.statusCode == 401 || result.statusCode == 403) invalidatePairing()
                    _state.value = _state.value.copy(
                        controlPending = false,
                        controlError = result.message,
                    )
                    false
                }
            }
        }
    }

    private suspend fun loadMedia(path: String, subtitleIndex: Int?, startPositionSeconds: Double) {
        player.stop()
        player.clearMediaItems()
        dataSourceFactory.setDefaultRequestProperties(mapOf("X-Secret-Key" to preferences.secretKey))
        val tracks = (api.fetchSubtitles(path) as? ApiResult.Success)?.value.orEmpty()
        val selectedSubtitleIndex = availableSubtitleIndex(subtitleIndex, tracks.map { it.index })
        val subtitleError = if (subtitleIndex != null && selectedSubtitleIndex == null) {
            "Subtitle track $subtitleIndex is unavailable"
        } else null
        _state.value = loadingState(_state.value, path, selectedSubtitleIndex).copy(controlError = subtitleError)
        reportObservation()
        val subtitles = tracks.map { track ->
            MediaItem.SubtitleConfiguration.Builder(Uri.parse(api.subtitleContentUrl(path, track.index)))
                .setId(track.index.toString())
                .setMimeType(MimeTypes.TEXT_VTT)
                .setLanguage(track.language)
                .setLabel(track.label.ifBlank { track.language.ifBlank { "Subtitles" } })
                .build()
        }
        player.setMediaItem(
            MediaItem.Builder()
                .setUri(api.streamUrl(path))
                .setSubtitleConfigurations(subtitles)
                .build(),
            (startPositionSeconds.coerceAtLeast(0.0) * 1_000).toLong(),
        )
        desiredSubtitleIndex = selectedSubtitleIndex
        player.trackSelectionParameters = player.trackSelectionParameters
            .buildUpon()
            .clearOverridesOfType(C.TRACK_TYPE_TEXT)
            .setTrackTypeDisabled(C.TRACK_TYPE_TEXT, selectedSubtitleIndex == null)
            .build()
        player.prepare()
        if (startPositionSeconds > 0) showSeekOverlay(startPositionSeconds)
    }

    private fun updateActualState() {
        val actual = when (player.playbackState) {
            Player.STATE_IDLE -> PlaybackObservationState.Idle
            Player.STATE_BUFFERING -> if (player.currentMediaItem == null) {
                PlaybackObservationState.Loading
            } else PlaybackObservationState.Buffering
            Player.STATE_ENDED -> PlaybackObservationState.Ended
            Player.STATE_READY -> if (player.isPlaying) PlaybackObservationState.Playing else PlaybackObservationState.Paused
            else -> PlaybackObservationState.Idle
        }
        updatePlayerSnapshot(actual)
        val duration = validDurationSeconds()
        if (duration > 0 && lastKnownDurationSeconds <= 0) reportProgress()
        lastKnownDurationSeconds = duration
        if (actual != lastReportedState) {
            lastReportedState = actual
            if (actual == PlaybackObservationState.Playing || actual == PlaybackObservationState.Paused) {
                showFeedback(actual)
            }
            reportObservation()
            if (actual in listOf(PlaybackObservationState.Paused, PlaybackObservationState.Ended)) reportProgress()
        }
    }

    private fun updatePlayerSnapshot(actual: PlaybackObservationState, error: PlaybackError? = null) {
        _state.value = _state.value.copy(
            actualState = actual,
            positionSeconds = player.currentPosition.coerceAtLeast(0) / 1_000.0,
            durationSeconds = validDurationSeconds(),
            error = error,
        )
    }

    private fun reportObservation() {
        captureObservation()?.let(observationQueue::request)
    }

    private fun captureObservation(): ReportPlaybackObservationRequest? {
        val session = activeSession ?: return null
        val snapshot = _state.value
        val sequence = nextObservationSequence(
            observationSequences[session.sessionId],
            session.observation?.observationSequence,
        )
        observationSequences[session.sessionId] = sequence
        return ReportPlaybackObservationRequest(
            sessionId = session.sessionId,
            observationSequence = sequence,
            appliedRevision = applied.revision.coerceAtLeast(0),
            appliedSeekId = applied.seekId,
            state = snapshot.actualState,
            relativePath = snapshot.relativePath,
            positionSeconds = snapshot.positionSeconds,
            durationSeconds = snapshot.durationSeconds,
            subtitleIndex = snapshot.subtitleIndex,
            errorCode = snapshot.error?.code,
            errorMessage = snapshot.error?.message,
        )
    }

    private fun maybeReportProgress() {
        if (isProgressDue(player.isPlaying, SystemClock.elapsedRealtime(), lastProgressMs)) reportProgress()
    }

    private fun reportProgress() {
        captureProgress()?.let(progressWriter::request)
    }

    private fun captureProgress(): ProgressSnapshot? {
        val path = _state.value.relativePath ?: return null
        val duration = validDurationSeconds()
        if (duration <= 0) return null
        val position = if (_state.value.actualState == PlaybackObservationState.Ended) duration
        else (player.currentPosition.coerceAtLeast(0) / 1_000.0).coerceAtMost(duration)
        return ProgressSnapshot(activeSession?.sessionId, path, position, duration)
    }

    private fun validDurationSeconds(): Double =
        player.duration.takeIf { it > 0 && it != C.TIME_UNSET }?.div(1_000.0) ?: 0.0

    private fun showSeekOverlay(positionSeconds: Double) {
        _state.value = _state.value.copy(seekDestinationSeconds = positionSeconds)
        viewModelScope.launch {
            delay(1_500)
            if (_state.value.seekDestinationSeconds == positionSeconds) {
                _state.value = _state.value.copy(seekDestinationSeconds = null)
            }
        }
    }

    private fun showFeedback(actual: PlaybackObservationState) {
        val feedback = if (actual == PlaybackObservationState.Playing) {
            PlaybackFeedback.Playing
        } else PlaybackFeedback.Paused
        _state.value = _state.value.copy(feedback = feedback)
        viewModelScope.launch {
            delay(1_500)
            if (_state.value.feedback == feedback) _state.value = _state.value.copy(feedback = null)
        }
    }

    private fun stopCurrentPlayback() {
        player.stop()
        activeSession = null
        applied = AppliedIntent()
        sessionGate.clear()
        lastProgressMs = null
        lastKnownDurationSeconds = 0.0
        _state.value = _state.value.copy(
            sessionId = null,
            relativePath = null,
            actualState = PlaybackObservationState.Idle,
            positionSeconds = 0.0,
            durationSeconds = 0.0,
            subtitleIndex = null,
            error = null,
        )
    }

    override fun onCleared() {
        val finalProgress = captureProgress()
        pollingJob?.cancel()
        backgroundPauseJob?.cancel()
        observationQueue.close()
        kotlinx.coroutines.runBlocking { progressWriter.close(finalProgress) }
        player.release()
        apiClient.close()
        super.onCleared()
    }

    private fun invalidatePairing() {
        preferences.clearPairing()
        foreground = false
        commandsEnabled = false
        setPollingEnabled(false)
        player.pause()
        _state.value = _state.value.copy(pairingInvalid = true)
    }

    private suspend fun refreshSession() {
        val generation = sessionGate.requestGeneration()
        val result = api.getPlaybackSession()
        if (result is ApiResult.Success && sessionGate.acceptPoll(generation, result.value.session)) {
            result.value.session?.let { applySession(it) }
        }
    }

    private fun applySubtitleSelection(tracks: androidx.media3.common.Tracks) {
        val index = desiredSubtitleIndex
        val builder = player.trackSelectionParameters.buildUpon()
            .clearOverridesOfType(C.TRACK_TYPE_TEXT)
            .setTrackTypeDisabled(C.TRACK_TYPE_TEXT, index == null)
        if (index != null) {
            tracks.groups.firstOrNull { group ->
                group.type == C.TRACK_TYPE_TEXT && (0 until group.length).any { group.getTrackFormat(it).id == index.toString() }
            }?.let { group ->
                val track = (0 until group.length).first { group.getTrackFormat(it).id == index.toString() }
                builder.addOverride(TrackSelectionOverride(group.mediaTrackGroup, track))
            }
        }
        player.trackSelectionParameters = builder.build()
    }
}

fun classifyError(error: PlaybackException): PlaybackError {
    val cause = error.cause
    if (cause is HttpDataSource.InvalidResponseCodeException) {
        return when (cause.responseCode) {
            401 -> PlaybackError.Unauthorized
            404 -> PlaybackError.FileNotFound
            else -> PlaybackError.Unknown("HTTP ${cause.responseCode}")
        }
    }
    if (cause is HttpDataSource.HttpDataSourceException) return PlaybackError.CannotReachServer()
    return PlaybackError.Unknown(error.message ?: "Playback error")
}
