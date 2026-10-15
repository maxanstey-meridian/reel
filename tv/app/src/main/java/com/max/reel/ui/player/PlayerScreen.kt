package com.max.reel.ui.player

import androidx.activity.compose.BackHandler
import androidx.compose.animation.AnimatedVisibility
import androidx.compose.animation.fadeIn
import androidx.compose.animation.fadeOut
import androidx.compose.foundation.background
import androidx.compose.foundation.focusable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.runtime.Composable
import androidx.compose.runtime.DisposableEffect
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.produceState
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.focus.FocusRequester
import androidx.compose.ui.focus.focusRequester
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.input.key.Key
import androidx.compose.ui.input.key.KeyEventType
import androidx.compose.ui.input.key.key
import androidx.compose.ui.input.key.onKeyEvent
import androidx.compose.ui.input.key.type
import androidx.compose.ui.platform.LocalView
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.compose.ui.viewinterop.AndroidView
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import androidx.media3.ui.PlayerView
import androidx.tv.material3.Button
import androidx.tv.material3.Text
import com.max.reel.data.PlaybackObservationState.Buffering
import com.max.reel.data.PlaybackObservationState.Ended
import com.max.reel.data.PlaybackObservationState.Loading
import com.max.reel.data.PlaybackObservationState.Paused
import com.max.reel.data.PlaybackObservationState.Playing
import com.max.reel.playback.PlaybackConnection
import com.max.reel.playback.PlaybackViewModel
import com.max.reel.ui.cast.DisconnectedOverlay
import com.max.reel.ui.theme.ReelColors
import kotlinx.coroutines.delay

@Composable
fun PlayerScreen(viewModel: PlaybackViewModel) {
    val state by viewModel.state.collectAsStateWithLifecycle()
    val view = LocalView.current
    val focusRequester = remember { FocusRequester() }
    val nextFocus = remember { FocusRequester() }
    val ended = state.actualState == Ended
    var showFeedback by remember { mutableStateOf(false) }
    var showSeek by remember { mutableStateOf(false) }
    var showNotice by remember { mutableStateOf(false) }

    // A short pause stays awake so the Fire TV screensaver doesn't cover the video; a long one
    // lets the screen sleep until playback resumes.
    val awake by produceState(state.actualState in AWAKE_STATES, state.actualState) {
        value = state.actualState in AWAKE_STATES
        if (state.actualState == Paused) {
            delay(PAUSED_AWAKE_MS)
            value = false
        }
    }
    DisposableEffect(awake) {
        view.keepScreenOn = awake
        onDispose { view.keepScreenOn = false }
    }
    LaunchedEffect(state.actualState) {
        showFeedback = state.actualState == Playing || state.actualState == Paused
        delay(1_500)
        showFeedback = false
    }
    LaunchedEffect(state.seekDestinationSeconds) {
        showSeek = state.seekDestinationSeconds != null
        delay(1_500)
        showSeek = false
    }
    LaunchedEffect(state.notice) {
        showNotice = state.notice != null
        delay(4_000)
        showNotice = false
    }
    BackHandler { viewModel.stopFromTv() }

    Box(
        modifier =
        Modifier
            .fillMaxSize()
            .background(Color.Black)
            .focusRequester(focusRequester)
            .onKeyEvent { event ->
                if (event.type != KeyEventType.KeyDown || state.error != null || ended) {
                    return@onKeyEvent false
                }
                when (event.key) {
                    Key.DirectionCenter, Key.MediaPlayPause -> viewModel.togglePlayPause()
                    Key.DirectionLeft, Key.MediaRewind -> viewModel.skip(-10.0)
                    Key.DirectionRight, Key.MediaFastForward -> viewModel.skip(10.0)
                    else -> return@onKeyEvent false
                }
                true
            }.focusable(),
    ) {
        AndroidView(
            factory = { context ->
                PlayerView(context).apply {
                    useController = false
                    isFocusable = false
                    player = viewModel.player
                }
            },
            onRelease = { it.player = null },
            modifier = Modifier.fillMaxSize(),
        )

        if (state.actualState == Loading || state.actualState == Buffering) {
            StatusOverlay(
                title = if (state.actualState == Loading) "Loading" else "Buffering",
                detail = state.title,
            )
        }

        AnimatedVisibility(
            visible = showFeedback,
            enter = fadeIn(),
            exit = fadeOut(),
            modifier = Modifier.align(Alignment.Center),
        ) {
            Text(
                text = if (state.actualState == Playing) "Playing" else "Paused",
                color = Color.White,
                fontSize = 28.sp,
                fontWeight = FontWeight.SemiBold,
                modifier =
                Modifier
                    .background(Color.Black.copy(alpha = 0.65f))
                    .padding(horizontal = 28.dp, vertical = 16.dp),
            )
        }

        state.seekDestinationSeconds?.takeIf { showSeek }?.let { destination ->
            Text(
                text = "Seeking to ${formatTimestamp((destination * 1_000).toLong())}",
                color = Color.White,
                fontSize = 22.sp,
                modifier =
                Modifier
                    .align(Alignment.BottomCenter)
                    .padding(bottom = 64.dp)
                    .background(Color.Black.copy(alpha = 0.75f))
                    .padding(horizontal = 24.dp, vertical = 12.dp),
            )
        }

        if (ended) {
            Column(
                modifier = Modifier.align(Alignment.Center),
                horizontalAlignment = Alignment.CenterHorizontally,
            ) {
                Text("Playback ended", color = Color.White, fontSize = 28.sp)
                if (state.nextEpisode?.relativePath != null) {
                    Button(
                        onClick = viewModel::playNext,
                        enabled = !state.controlPending,
                        modifier = Modifier.focusRequester(nextFocus),
                    ) {
                        Text("Play next: ${state.nextEpisode?.title}")
                    }
                }
            }
        }

        state.error?.let { error ->
            ErrorOverlay(message = error.message, onBack = viewModel::stopFromTv)
        }
        state.notice?.takeIf { showNotice }?.let { notice ->
            Text(
                text = notice,
                color = ReelColors.disconnected,
                fontSize = 16.sp,
                modifier =
                Modifier
                    .align(Alignment.TopCenter)
                    .padding(top = 32.dp)
                    .background(Color.Black.copy(alpha = 0.8f))
                    .padding(horizontal = 20.dp, vertical = 10.dp),
            )
        }
        DisconnectedOverlay(
            visible = state.connection == PlaybackConnection.Disconnected,
            error = state.connectionError,
        )
    }

    // Overlay buttons take focus; when they disappear focus must return to the key-handling box.
    val hasError = state.error != null
    LaunchedEffect(hasError, state.nextEpisode?.relativePath) {
        when {
            hasError -> Unit
            state.nextEpisode?.relativePath != null -> nextFocus.requestFocus()
            else -> focusRequester.requestFocus()
        }
    }
}

@Composable
private fun StatusOverlay(title: String, detail: String?) {
    Column(
        modifier =
        Modifier
            .fillMaxSize()
            .background(Color.Black.copy(alpha = 0.45f)),
        horizontalAlignment = Alignment.CenterHorizontally,
        verticalArrangement = Arrangement.Center,
    ) {
        Text(title, color = Color.White, fontSize = 30.sp, fontWeight = FontWeight.Bold)
        if (detail != null) {
            Spacer(Modifier.height(8.dp))
            Text(detail, color = ReelColors.textSecondary, fontSize = 16.sp)
        }
    }
}

@Composable
private fun ErrorOverlay(message: String, onBack: () -> Unit) {
    val backFocus = remember { FocusRequester() }
    LaunchedEffect(Unit) { backFocus.requestFocus() }
    Column(
        modifier =
        Modifier
            .fillMaxSize()
            .background(Color.Black.copy(alpha = 0.85f)),
        horizontalAlignment = Alignment.CenterHorizontally,
        verticalArrangement = Arrangement.Center,
    ) {
        Text("Playback failed", color = ReelColors.disconnected, fontSize = 30.sp)
        Spacer(Modifier.height(12.dp))
        Text(message, color = Color.White, fontSize = 18.sp)
        Spacer(Modifier.height(24.dp))
        Button(onClick = onBack, modifier = Modifier.focusRequester(backFocus)) { Text("Back") }
    }
}

internal fun formatTimestamp(milliseconds: Long): String {
    val total = (milliseconds / 1_000).coerceAtLeast(0)
    val hours = total / 3_600
    val minutes = total % 3_600 / 60
    val remainder = total % 60
    return if (hours > 0) {
        "%d:%02d:%02d".format(hours, minutes, remainder)
    } else {
        "%d:%02d".format(minutes, remainder)
    }
}

private const val PAUSED_AWAKE_MS = 20 * 60 * 1_000L
private val AWAKE_STATES = setOf(Playing, Paused, Loading, Buffering)
