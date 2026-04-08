package com.max.reel.ui.pairing

import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.runtime.Composable
import androidx.compose.runtime.DisposableEffect
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.lifecycle.ViewModel
import androidx.lifecycle.ViewModelProvider
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import androidx.lifecycle.viewModelScope
import androidx.lifecycle.viewmodel.compose.viewModel
import androidx.tv.material3.ExperimentalTvMaterial3Api
import androidx.tv.material3.Text
import com.max.reel.data.ApiClient
import com.max.reel.data.ConnectResult
import com.max.reel.data.DiscoveredServer
import com.max.reel.data.PreferencesManager
import com.max.reel.data.ServerDiscovery
import com.max.reel.ui.theme.ReelColors
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.filterNotNull
import kotlinx.coroutines.flow.first
import kotlinx.coroutines.flow.update
import kotlinx.coroutines.launch

data class PairingState(
    val error: String? = null,
)

class PairingViewModel(
    private val apiClient: ApiClient,
    private val prefs: PreferencesManager,
    private val discovery: ServerDiscovery,
) : ViewModel() {

    private val _state = MutableStateFlow(PairingState())
    val state: StateFlow<PairingState> = _state.asStateFlow()

    private var onSuccessCallback: (() -> Unit)? = null

    fun setOnSuccess(callback: () -> Unit) {
        onSuccessCallback = callback
    }

    fun startDiscovery(context: android.content.Context) {
        discovery.start(context)

        viewModelScope.launch {
            // Wait indefinitely for a server to appear
            val found = discovery.server.filterNotNull().first()
            autoConnect(found)
        }
    }

    fun stopDiscovery() {
        discovery.stop()
    }

    private fun autoConnect(server: DiscoveredServer) {
        viewModelScope.launch {
            when (apiClient.connect(server.ip, server.port.toString(), server.secretKey)) {
                is ConnectResult.Success -> {
                    prefs.savePairing(server.ip, server.port.toString(), server.secretKey)
                    onSuccessCallback?.invoke()
                }
                is ConnectResult.Unauthorized -> {
                    _state.update { it.copy(error = "Server found but key was rejected") }
                }
                is ConnectResult.NetworkError -> {
                    _state.update { it.copy(error = "Server found but can't connect") }
                }
            }
        }
    }
}

@OptIn(ExperimentalTvMaterial3Api::class)
@Composable
fun PairingScreen(
    apiClient: ApiClient,
    prefs: PreferencesManager,
    discovery: ServerDiscovery,
    onPaired: () -> Unit,
) {
    val viewModel: PairingViewModel = viewModel(
        factory = object : ViewModelProvider.Factory {
            @Suppress("UNCHECKED_CAST")
            override fun <T : ViewModel> create(modelClass: Class<T>): T {
                return PairingViewModel(apiClient, prefs, discovery) as T
            }
        }
    )

    val state by viewModel.state.collectAsStateWithLifecycle()
    val context = LocalContext.current

    LaunchedEffect(Unit) {
        viewModel.setOnSuccess(onPaired)
        viewModel.startDiscovery(context)
    }

    DisposableEffect(Unit) {
        onDispose { viewModel.stopDiscovery() }
    }

    Column(
        modifier = Modifier
            .fillMaxSize()
            .background(ReelColors.backgroundGradient)
            .padding(32.dp),
        horizontalAlignment = Alignment.CenterHorizontally,
        verticalArrangement = Arrangement.Center,
    ) {
        Text(
            text = "Searching for Reel server…",
            fontSize = 18.sp,
            color = ReelColors.textSecondary,
        )

        Spacer(modifier = Modifier.height(24.dp))

        CircularProgressIndicator(
            modifier = Modifier.size(32.dp),
            strokeWidth = 3.dp,
            color = ReelColors.textSecondary,
        )

        state.error?.let { error ->
            Spacer(modifier = Modifier.height(24.dp))
            Text(
                text = error,
                fontSize = 14.sp,
                color = ReelColors.disconnected,
            )
        }
    }
}
