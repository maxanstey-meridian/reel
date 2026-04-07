package com.max.reel.ui.player

import android.net.Uri
import androidx.activity.compose.BackHandler
import androidx.annotation.OptIn
import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.runtime.Composable
import androidx.compose.runtime.DisposableEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.unit.dp
import androidx.media3.common.MediaItem
import androidx.media3.common.PlaybackException
import androidx.media3.common.Player
import androidx.media3.common.util.UnstableApi
import androidx.media3.datasource.DefaultHttpDataSource
import androidx.media3.exoplayer.ExoPlayer
import androidx.media3.exoplayer.source.ProgressiveMediaSource
import androidx.media3.ui.compose.ContentFrame
import androidx.media3.ui.compose.SURFACE_TYPE_SURFACE_VIEW
import androidx.tv.material3.ExperimentalTvMaterial3Api
import androidx.tv.material3.Surface
import androidx.tv.material3.Text
import com.max.reel.data.PreferencesManager

sealed interface PlayerError {
    data object CannotReachServer : PlayerError
    data object Unauthorized : PlayerError
    data object FileNotFound : PlayerError
    data class Unknown(val message: String) : PlayerError
}

private fun classifyError(error: PlaybackException): PlayerError {
    val cause = error.cause
    if (cause is androidx.media3.datasource.HttpDataSource.InvalidResponseCodeException) {
        return when (cause.responseCode) {
            401 -> PlayerError.Unauthorized
            404 -> PlayerError.FileNotFound
            else -> PlayerError.Unknown("HTTP ${cause.responseCode}")
        }
    }
    if (cause is androidx.media3.datasource.HttpDataSource.HttpDataSourceException) {
        return PlayerError.CannotReachServer
    }
    return PlayerError.Unknown(error.message ?: "Playback error")
}

private fun playerErrorMessage(error: PlayerError): String = when (error) {
    is PlayerError.CannotReachServer -> "Can't reach server"
    is PlayerError.Unauthorized -> "Invalid key"
    is PlayerError.FileNotFound -> "File not found"
    is PlayerError.Unknown -> error.message
}

@OptIn(UnstableApi::class)
@ExperimentalTvMaterial3Api
@Composable
fun PlayerScreen(prefs: PreferencesManager, relativePath: String, onBack: () -> Unit) {
    val context = LocalContext.current
    val streamUrl = "http://${prefs.serverIp}:${prefs.serverPort}/api/files/stream?path=${Uri.encode(relativePath)}"

    var playerError by remember { mutableStateOf<PlayerError?>(null) }

    val player = remember {
        ExoPlayer.Builder(context).build()
    }

    DisposableEffect(player) {
        val dataSourceFactory = DefaultHttpDataSource.Factory()
            .setDefaultRequestProperties(mapOf("X-Secret-Key" to prefs.secretKey))

        val mediaSource = ProgressiveMediaSource.Factory(dataSourceFactory)
            .createMediaSource(MediaItem.fromUri(streamUrl))

        val listener = object : Player.Listener {
            override fun onPlayerError(error: PlaybackException) {
                playerError = classifyError(error)
            }
        }
        player.addListener(listener)
        player.setMediaSource(mediaSource)
        player.prepare()
        player.playWhenReady = true

        onDispose {
            player.stop()
            player.release()
        }
    }

    BackHandler {
        onBack()
    }

    Box(modifier = Modifier.fillMaxSize().background(Color.Black)) {
        val error = playerError
        if (error != null) {
            PlayerErrorView(
                message = playerErrorMessage(error),
                onBack = onBack,
            )
        } else {
            ContentFrame(
                player = player,
                surfaceType = SURFACE_TYPE_SURFACE_VIEW,
                modifier = Modifier.fillMaxSize(),
            )
        }
    }
}

@ExperimentalTvMaterial3Api
@Composable
private fun PlayerErrorView(message: String, onBack: () -> Unit) {
    Column(
        modifier = Modifier
            .fillMaxSize()
            .padding(32.dp),
        horizontalAlignment = Alignment.CenterHorizontally,
        verticalArrangement = Arrangement.Center,
    ) {
        Text(text = message, color = Color.Red)
        Spacer(modifier = Modifier.height(16.dp))
        Surface(onClick = onBack) {
            Text(
                text = "Back",
                modifier = Modifier.padding(horizontal = 24.dp, vertical = 12.dp),
            )
        }
    }
}
