package com.max.reel.ui.player

import android.net.Uri
import androidx.activity.compose.BackHandler
import androidx.annotation.OptIn
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
import androidx.compose.runtime.key
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.focus.FocusRequester
import androidx.compose.ui.focus.focusRequester
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.input.key.KeyEventType
import androidx.compose.ui.input.key.key
import androidx.compose.ui.input.key.onKeyEvent
import androidx.compose.ui.input.key.type
import androidx.compose.ui.input.key.Key
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.unit.dp
import androidx.media3.common.C
import androidx.media3.common.MediaItem
import androidx.media3.common.MimeTypes
import androidx.media3.common.PlaybackException
import androidx.media3.common.Player
import androidx.media3.common.util.UnstableApi
import androidx.media3.datasource.DefaultHttpDataSource
import androidx.media3.exoplayer.ExoPlayer
import androidx.media3.exoplayer.source.DefaultMediaSourceFactory
import androidx.media3.ui.compose.ContentFrame
import androidx.media3.ui.compose.SURFACE_TYPE_SURFACE_VIEW
import androidx.tv.material3.ExperimentalTvMaterial3Api
import androidx.tv.material3.Surface
import androidx.tv.material3.Text
import com.max.reel.data.ApiClient
import com.max.reel.data.PreferencesManager
import kotlinx.coroutines.NonCancellable
import kotlinx.coroutines.delay
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext

sealed interface PlayerError {
    data object CannotReachServer : PlayerError
    data object Unauthorized : PlayerError
    data object FileNotFound : PlayerError
    data class Unknown(val message: String) : PlayerError
}

internal fun classifyError(error: PlaybackException): PlayerError {
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

internal fun playerErrorMessage(error: PlayerError): String = when (error) {
    is PlayerError.CannotReachServer -> "Can't reach server"
    is PlayerError.Unauthorized -> "Invalid key"
    is PlayerError.FileNotFound -> "File not found"
    is PlayerError.Unknown -> error.message
}

@OptIn(UnstableApi::class)
@ExperimentalTvMaterial3Api
@Composable
fun PlayerScreen(
    prefs: PreferencesManager,
    apiClient: ApiClient,
    relativePath: String,
    positionSeconds: Double? = null,
    subtitleIndex: Int? = null,
    onBack: () -> Unit,
    onPlayNext: (relativePath: String) -> Unit = {},
) {
    val context = LocalContext.current

    BackHandler {
        onBack()
    }

    val focusRequester = remember { FocusRequester() }
    val seekStepMs = 10_000L
    val scope = rememberCoroutineScope()

    key(relativePath) {
        val streamUrl = "http://${prefs.serverIp}:${prefs.serverPort}/api/files/stream?path=${Uri.encode(relativePath)}"

        var playerError by remember { mutableStateOf<PlayerError?>(null) }
        var markedWatched by remember { mutableStateOf(false) }

        val dataSourceFactory = remember {
            DefaultHttpDataSource.Factory()
                .setDefaultRequestProperties(mapOf("X-Secret-Key" to prefs.secretKey))
        }

        val player = remember {
            ExoPlayer.Builder(context)
                .setMediaSourceFactory(DefaultMediaSourceFactory(dataSourceFactory))
                .build()
        }

        // Helper to report current progress
        fun reportProgress() {
            val posMs = player.currentPosition
            val durMs = player.duration
            if (durMs <= 0 || durMs == C.TIME_UNSET) return
            val posSec = posMs / 1000.0
            val durSec = durMs / 1000.0
            scope.launch {
                apiClient.updateProgress(relativePath, posSec, durSec)
                // Auto-mark watched at >95% completion
                if (!markedWatched && posSec / durSec > 0.95) {
                    markedWatched = true
                    apiClient.setWatched(relativePath)
                }
            }
        }

        // Fetch subtitles and configure media item
        LaunchedEffect(Unit) {
            val tracks = apiClient.fetchSubtitles(relativePath)
            val selectedTracks = if (subtitleIndex != null) {
                tracks.filter { it.index == subtitleIndex }
            } else {
                tracks.take(1)
            }
            val subtitleConfigs = selectedTracks.map { track ->
                MediaItem.SubtitleConfiguration.Builder(
                    Uri.parse(apiClient.subtitleContentUrl(relativePath, track.index))
                )
                    .setMimeType(MimeTypes.TEXT_VTT)
                    .setLanguage(track.language)
                    .setLabel(track.label.ifEmpty { track.language.ifEmpty { "Subtitles" } })
                    .setSelectionFlags(C.SELECTION_FLAG_DEFAULT)
                    .build()
            }

            val mediaItem = MediaItem.Builder()
                .setUri(streamUrl)
                .setSubtitleConfigurations(subtitleConfigs)
                .build()

            player.setMediaItem(mediaItem)
            player.prepare()
        }

        DisposableEffect(Unit) {

            val listener = object : Player.Listener {
                override fun onPlayerError(error: PlaybackException) {
                    playerError = classifyError(error)
                }

                override fun onIsPlayingChanged(isPlaying: Boolean) {
                    // Report on pause (isPlaying goes false while not in ended state)
                    if (!isPlaying && player.playbackState != Player.STATE_ENDED) {
                        reportProgress()
                    }
                }

                override fun onPlaybackStateChanged(playbackState: Int) {
                    when (playbackState) {
                        Player.STATE_ENDED -> {
                            reportProgress()
                            if (prefs.autoAdvance) {
                                scope.launch {
                                    val queue = apiClient.fetchCastQueue()
                                    if (queue.isEmpty()) {
                                        val next = apiClient.nextFile(relativePath)
                                        if (next != null) {
                                            onPlayNext(next.relativePath)
                                        }
                                    }
                                }
                            }
                        }
                        Player.STATE_READY -> {
                            // Seek to resume position once media is ready
                            if (positionSeconds != null && positionSeconds > 0) {
                                val seekMs = (positionSeconds * 1000).toLong()
                                if (player.currentPosition < seekMs - 1000) {
                                    player.seekTo(seekMs)
                                }
                            }
                        }
                        else -> {}
                    }
                }
            }
            player.addListener(listener)
            player.playWhenReady = true

            onDispose {
                // Capture position synchronously before releasing the player
                val posMs = player.currentPosition
                val durMs = player.duration
                player.stop()
                player.release()

                // Launch with NonCancellable so the final report survives scope cancellation
                if (durMs > 0 && durMs != C.TIME_UNSET) {
                    scope.launch(NonCancellable) {
                        withContext(NonCancellable) {
                            apiClient.updateProgress(relativePath, posMs / 1000.0, durMs / 1000.0)
                            if (!markedWatched && (posMs / 1000.0) / (durMs / 1000.0) > 0.95) {
                                apiClient.setWatched(relativePath)
                            }
                        }
                    }
                }
            }
        }

        // Periodic progress reporting every 10 seconds
        LaunchedEffect(Unit) {
            while (true) {
                delay(10_000)
                if (player.isPlaying) {
                    reportProgress()
                }
            }
        }

        Box(
            modifier = Modifier
                .fillMaxSize()
                .background(Color.Black)
                .focusRequester(focusRequester)
                .onKeyEvent { event ->
                    if (event.type != KeyEventType.KeyDown) return@onKeyEvent false
                    when (event.key) {
                        Key.DirectionCenter, Key.MediaPlayPause -> {
                            if (player.isPlaying) player.pause() else player.play()
                            true
                        }
                        Key.DirectionRight, Key.MediaFastForward -> {
                            player.seekTo(player.currentPosition + seekStepMs)
                            true
                        }
                        Key.DirectionLeft, Key.MediaRewind -> {
                            player.seekTo((player.currentPosition - seekStepMs).coerceAtLeast(0))
                            true
                        }
                        else -> false
                    }
                }
                .focusable()
        ) {
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

        LaunchedEffect(Unit) {
            focusRequester.requestFocus()
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
