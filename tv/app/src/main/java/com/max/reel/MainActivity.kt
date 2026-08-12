package com.max.reel

import android.os.Bundle
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.activity.viewModels
import androidx.compose.runtime.Composable
import androidx.compose.runtime.DisposableEffect
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import androidx.lifecycle.compose.LifecycleEventEffect
import androidx.lifecycle.Lifecycle
import androidx.tv.material3.ExperimentalTvMaterial3Api
import androidx.tv.material3.Surface
import com.max.reel.data.ServerDiscovery
import com.max.reel.playback.AppDestination
import com.max.reel.playback.PlaybackConnection
import com.max.reel.playback.PlaybackViewModel
import com.max.reel.playback.appDestination
import com.max.reel.ui.pairing.PairingScreen
import com.max.reel.ui.player.PlayerScreen
import com.max.reel.ui.ready.ReadyScreen

class MainActivity : ComponentActivity() {

    private val playbackViewModel: PlaybackViewModel by viewModels()

    @OptIn(ExperimentalTvMaterial3Api::class)
    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)

        val discovery = ServerDiscovery()

        setContent {
            ReelApp(
                viewModel = playbackViewModel,
                discovery = discovery,
            )
        }
    }
}

@OptIn(ExperimentalTvMaterial3Api::class)
@Composable
private fun ReelApp(
    viewModel: PlaybackViewModel,
    discovery: ServerDiscovery,
) {
    val playbackState by viewModel.state.collectAsStateWithLifecycle()
    val paired = remember { mutableStateOf(viewModel.preferences.isPaired) }

    LaunchedEffect(playbackState.pairingInvalid) {
        if (playbackState.pairingInvalid) paired.value = false
    }

    DisposableEffect(paired.value) {
        viewModel.setPollingEnabled(paired.value)
        onDispose { viewModel.setPollingEnabled(false) }
    }
    LifecycleEventEffect(Lifecycle.Event.ON_START) {
        viewModel.resumeAfterBackground(paired.value)
    }
    LifecycleEventEffect(Lifecycle.Event.ON_STOP) {
        viewModel.pauseForBackground()
    }

    when (appDestination(paired.value, playbackState.relativePath)) {
        AppDestination.Pairing -> Surface {
            PairingScreen(
                apiClient = viewModel.apiClient,
                prefs = viewModel.preferences,
                discovery = discovery,
                onPaired = {
                    paired.value = true
                    viewModel.onPaired()
                },
            )
        }

        AppDestination.Ready -> Surface {
            ReadyScreen(
                prefs = viewModel.preferences,
                connected = playbackState.connection is PlaybackConnection.Connected,
                onForgetPairing = {
                    viewModel.forgetPairing()
                    paired.value = false
                },
            )
        }

        AppDestination.Player -> PlayerScreen(viewModel)
    }
}
