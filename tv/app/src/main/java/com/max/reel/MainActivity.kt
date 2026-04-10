package com.max.reel

import android.os.Bundle
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.lifecycle.ViewModel
import androidx.lifecycle.ViewModelProvider
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import androidx.lifecycle.viewmodel.compose.viewModel
import androidx.tv.material3.ExperimentalTvMaterial3Api
import androidx.tv.material3.Surface
import coil.Coil
import coil.ImageLoader
import com.max.reel.data.ApiClient
import com.max.reel.data.PreferencesManager
import com.max.reel.data.ServerDiscovery
import com.max.reel.ui.cast.WaitingForCastScreen
import com.max.reel.ui.pairing.PairingScreen
import com.max.reel.ui.player.PlayerScreen
import okhttp3.OkHttpClient

sealed interface Screen {
    data object Pairing : Screen
    data object WaitingForCast : Screen
    data class Player(val relativePath: String, val positionSeconds: Double? = null, val subtitleIndex: Int? = null, val isQueuePlayback: Boolean = false) : Screen
}

class MainActivity : ComponentActivity() {

    private lateinit var apiClient: ApiClient

    @OptIn(ExperimentalTvMaterial3Api::class)
    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)

        val prefs = PreferencesManager(applicationContext)
        val discovery = ServerDiscovery()
        apiClient = ApiClient(prefs)

        Coil.setImageLoader(
            ImageLoader.Builder(this)
                .okHttpClient {
                    OkHttpClient.Builder()
                        .addInterceptor { chain ->
                            chain.proceed(
                                chain.request().newBuilder()
                                    .header("X-Secret-Key", prefs.secretKey)
                                    .build()
                            )
                        }
                        .build()
                }
                .crossfade(true)
                .build()
        )

        setContent {
            ReelApp(
                startPaired = prefs.isPaired,
                prefs = prefs,
                apiClient = apiClient,
                discovery = discovery,
            )
        }
    }

    override fun onDestroy() {
        super.onDestroy()
        apiClient.close()
    }
}

@OptIn(ExperimentalTvMaterial3Api::class)
@Composable
private fun ReelApp(
    startPaired: Boolean,
    prefs: PreferencesManager,
    apiClient: ApiClient,
    discovery: ServerDiscovery,
) {
    var screen: Screen by remember {
        mutableStateOf(
            if (startPaired) Screen.WaitingForCast else Screen.Pairing
        )
    }

    val castPoller: CastPoller = viewModel(
        factory = object : ViewModelProvider.Factory {
            @Suppress("UNCHECKED_CAST")
            override fun <T : ViewModel> create(modelClass: Class<T>): T {
                return CastPoller(apiClient) as T
            }
        }
    )

    // Start polling once paired, keep polling across all screens
    LaunchedEffect(screen) {
        if (screen is Screen.Pairing) {
            castPoller.stopPolling()
        } else {
            castPoller.startPolling()
        }
    }

    // React to play commands from any screen
    LaunchedEffect(Unit) {
        castPoller.playCommand.collect { command ->
            screen = Screen.Player(command.relativePath, command.positionSeconds, command.subtitleIndex)
        }
    }

    val connectionOk by castPoller.connectionOk.collectAsStateWithLifecycle()

    when (val current = screen) {
        is Screen.Pairing -> Surface {
            PairingScreen(
                apiClient = apiClient,
                prefs = prefs,
                discovery = discovery,
                onPaired = { screen = Screen.WaitingForCast },
            )
        }

        is Screen.WaitingForCast -> Surface {
            WaitingForCastScreen(
                connectionOk = connectionOk,
                prefs = prefs,
                apiClient = apiClient,
            )
        }

        is Screen.Player -> PlayerScreen(
            prefs = prefs,
            apiClient = apiClient,
            relativePath = current.relativePath,
            positionSeconds = current.positionSeconds,
            subtitleIndex = current.subtitleIndex,
            isQueuePlayback = current.isQueuePlayback,
            onBack = { screen = Screen.WaitingForCast },
            onFileEnded = { nextPath ->
                screen = if (nextPath != null) Screen.Player(nextPath) else Screen.WaitingForCast
            },
        )
    }
}
