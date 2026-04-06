package com.max.reel

import android.os.Bundle
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.tv.material3.ExperimentalTvMaterial3Api
import androidx.tv.material3.Surface
import com.max.reel.data.ApiClient
import com.max.reel.data.PreferencesManager
import com.max.reel.ui.library.LibraryScreen
import com.max.reel.ui.pairing.PairingScreen
import com.max.reel.ui.player.PlayerScreen

sealed interface Screen {
    data object Pairing : Screen
    data object Library : Screen
    data class Player(val relativePath: String) : Screen
}

class MainActivity : ComponentActivity() {

    private lateinit var apiClient: ApiClient

    @OptIn(ExperimentalTvMaterial3Api::class)
    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)

        val prefs = PreferencesManager(applicationContext)
        apiClient = ApiClient(prefs)

        setContent {
            ReelApp(
                startPaired = prefs.isPaired,
                prefs = prefs,
                apiClient = apiClient,
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
) {
    var screen: Screen by remember {
        mutableStateOf(
            if (startPaired) Screen.Library else Screen.Pairing
        )
    }

    Surface {
        when (val current = screen) {
            is Screen.Pairing -> PairingScreen(
                apiClient = apiClient,
                prefs = prefs,
                onPaired = { screen = Screen.Library },
            )

            is Screen.Library -> LibraryScreen(
                apiClient = apiClient,
                onPlayFile = { relativePath -> screen = Screen.Player(relativePath) },
            )

            is Screen.Player -> PlayerScreen(
                prefs = prefs,
                relativePath = current.relativePath,
                onBack = { screen = Screen.Library },
            )
        }
    }
}
