package com.max.reel

import android.os.Bundle
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.activity.viewModels
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.lifecycle.Lifecycle
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import androidx.lifecycle.lifecycleScope
import androidx.lifecycle.repeatOnLifecycle
import androidx.lifecycle.viewmodel.initializer
import androidx.lifecycle.viewmodel.viewModelFactory
import androidx.tv.material3.Surface
import com.max.reel.data.ApiClient
import com.max.reel.data.PairingStore
import com.max.reel.data.discoverServers
import com.max.reel.playback.PlaybackConnection
import com.max.reel.playback.PlaybackViewModel
import com.max.reel.ui.pairing.PairingScreen
import com.max.reel.ui.player.PlayerScreen
import com.max.reel.ui.ready.ReadyScreen
import kotlinx.coroutines.launch

class MainActivity : ComponentActivity() {
    private val app get() = application as ReelApplication
    private val viewModel: PlaybackViewModel by viewModels {
        viewModelFactory {
            initializer { PlaybackViewModel(app, app.pairing, app.api) { discoverServers(app) } }
        }
    }

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        lifecycleScope.launch { repeatOnLifecycle(Lifecycle.State.STARTED) { viewModel.poll() } }
        setContent { ReelApp(app.pairing, app.api, viewModel) }
    }

    override fun onStop() {
        super.onStop()
        viewModel.pauseForBackground()
    }
}

@Composable
private fun ReelApp(pairing: PairingStore, api: ApiClient, viewModel: PlaybackViewModel) {
    val server by pairing.server.collectAsStateWithLifecycle()
    val state by viewModel.state.collectAsStateWithLifecycle()
    val paired = server
    when {
        paired == null -> {
            Surface { PairingScreen(pairing, api::connect) }
        }

        state.relativePath != null -> {
            PlayerScreen(viewModel)
        }

        else -> {
            Surface {
                ReadyScreen(
                    server = paired,
                    pairUrl = api.pairUrl(paired),
                    connection = state.connection,
                    connectionError = state.connectionError,
                    onForgetPairing = pairing::forget,
                )
            }
        }
    }
}
