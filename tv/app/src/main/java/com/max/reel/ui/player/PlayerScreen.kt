package com.max.reel.ui.player

import androidx.activity.compose.BackHandler
import androidx.annotation.OptIn
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
import androidx.compose.runtime.remember
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
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import androidx.media3.common.util.UnstableApi
import androidx.media3.ui.compose.ContentFrame
import androidx.media3.ui.compose.SURFACE_TYPE_SURFACE_VIEW
import androidx.tv.material3.Button
import androidx.tv.material3.ExperimentalTvMaterial3Api
import androidx.tv.material3.Text
import com.max.reel.data.PlaybackObservationState
import com.max.reel.playback.PlaybackConnection
import com.max.reel.playback.PlaybackFeedback
import com.max.reel.playback.PlaybackViewModel
import com.max.reel.ui.cast.DisconnectedOverlay
import com.max.reel.ui.theme.ReelColors

@OptIn(UnstableApi::class)
@ExperimentalTvMaterial3Api
@Composable
fun PlayerScreen(viewModel: PlaybackViewModel) {
    val state by viewModel.state.collectAsStateWithLifecycle()
    val view = LocalView.current
    val focusRequester = remember { FocusRequester() }

    DisposableEffect(Unit) {
        view.keepScreenOn = true
        onDispose { view.keepScreenOn = false }
    }
    BackHandler { viewModel.stopFromTv() }

    Box(
        modifier = Modifier
            .fillMaxSize()
            .background(Color.Black)
            .focusRequester(focusRequester)
            .onKeyEvent { event ->
                if (state.error != null) return@onKeyEvent false
                if (event.type == KeyEventType.KeyDown &&
                    event.key in listOf(Key.DirectionCenter, Key.MediaPlayPause)
                ) {
                    viewModel.togglePlayPause()
                    true
                } else false
            }
            .focusable(),
    ) {
        ContentFrame(
            player = viewModel.player,
            surfaceType = SURFACE_TYPE_SURFACE_VIEW,
            modifier = Modifier.fillMaxSize(),
        )

        if (state.actualState in listOf(PlaybackObservationState.Loading, PlaybackObservationState.Buffering)) {
            StatusOverlay(
                title = if (state.actualState == PlaybackObservationState.Loading) "Loading" else "Buffering",
                detail = state.relativePath?.substringAfterLast('/'),
            )
        }

        AnimatedVisibility(
            visible = state.feedback != null,
            enter = fadeIn(),
            exit = fadeOut(),
            modifier = Modifier.align(Alignment.Center),
        ) {
            Text(
                text = if (state.feedback == PlaybackFeedback.Playing) "Playing" else "Paused",
                color = Color.White,
                fontSize = 28.sp,
                fontWeight = FontWeight.SemiBold,
                modifier = Modifier
                    .background(Color.Black.copy(alpha = 0.65f))
                    .padding(horizontal = 28.dp, vertical = 16.dp),
            )
        }

        state.seekDestinationSeconds?.let { destination ->
            Text(
                text = "Seeking to ${formatTimestamp((destination * 1_000).toLong())}",
                color = Color.White,
                fontSize = 22.sp,
                modifier = Modifier
                    .align(Alignment.BottomCenter)
                    .padding(bottom = 64.dp)
                    .background(Color.Black.copy(alpha = 0.75f))
                    .padding(horizontal = 24.dp, vertical = 12.dp),
            )
        }

        state.error?.let { error ->
            ErrorOverlay(message = error.message, onBack = viewModel::stopFromTv)
        }
        state.controlError?.let { message ->
            Text(
                text = "Control failed: $message",
                color = ReelColors.disconnected,
                fontSize = 16.sp,
                modifier = Modifier
                    .align(Alignment.TopCenter)
                    .padding(top = 32.dp)
                    .background(Color.Black.copy(alpha = 0.8f))
                    .padding(horizontal = 20.dp, vertical = 10.dp),
            )
        }
        DisconnectedOverlay(visible = state.connection is PlaybackConnection.Retrying)
    }

    LaunchedEffect(Unit) { focusRequester.requestFocus() }
}

@Composable
private fun StatusOverlay(title: String, detail: String?) {
    Column(
        modifier = Modifier
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

@OptIn(ExperimentalTvMaterial3Api::class)
@Composable
private fun ErrorOverlay(message: String, onBack: () -> Unit) {
    val backFocus = remember { FocusRequester() }
    LaunchedEffect(Unit) { backFocus.requestFocus() }
    Column(
        modifier = Modifier
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
    return if (hours > 0) "%d:%02d:%02d".format(hours, minutes, remainder)
    else "%d:%02d".format(minutes, remainder)
}
