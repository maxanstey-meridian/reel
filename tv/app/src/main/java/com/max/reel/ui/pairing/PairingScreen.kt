package com.max.reel.ui.pairing

import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.runtime.Composable
import androidx.compose.runtime.DisposableEffect
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.focus.focusRequester
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.lifecycle.ViewModel
import androidx.lifecycle.ViewModelProvider
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import androidx.lifecycle.viewModelScope
import androidx.lifecycle.viewmodel.compose.viewModel
import androidx.tv.material3.ExperimentalTvMaterial3Api
import androidx.tv.material3.Button
import androidx.tv.material3.Text
import com.max.reel.data.ApiClient
import com.max.reel.data.ConnectResult
import com.max.reel.data.DiscoveredServer
import com.max.reel.data.PreferencesManager
import com.max.reel.data.ServerDiscoverer
import com.max.reel.ui.theme.ReelColors
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.Job
import kotlinx.coroutines.CoroutineStart
import kotlinx.coroutines.flow.update
import kotlinx.coroutines.launch

data class PairingState(
    val error: String? = null,
    val connecting: Boolean = false,
)

class PairingViewModel(
    private val apiClient: ApiClient,
    private val prefs: PreferencesManager,
    private val discovery: ServerDiscoverer,
    private val connect: suspend (String, String, String) -> ConnectResult = apiClient::connect,
) : ViewModel() {

    private val _state = MutableStateFlow(PairingState())
    val state: StateFlow<PairingState> = _state.asStateFlow()

    private var onSuccessCallback: (() -> Unit)? = null
    private var discoveryJob: Job? = null
    private var connectJob: Job? = null
    private var connectGeneration = 0L
    private var lastServer: DiscoveredServer? = null

    fun setOnSuccess(callback: () -> Unit) {
        onSuccessCallback = callback
    }

    fun startDiscovery(context: android.content.Context) {
        if (discoveryJob != null) return
        discoveryJob = viewModelScope.launch(start = CoroutineStart.UNDISPATCHED) {
            discovery.servers.collect { found ->
                lastServer = found
                startConnect(found)
            }
        }
        discovery.start(context)
    }

    fun stopDiscovery() {
        discoveryJob?.cancel()
        discoveryJob = null
        discovery.stop()
    }

    fun retry() {
        lastServer?.let { server ->
            startConnect(server)
        }
    }

    fun connectManually(host: String, port: String, key: String) {
        val parsedPort = port.toIntOrNull()
        if (host.isBlank() || key.isBlank() || parsedPort == null || parsedPort !in 1..65535) {
            _state.update { it.copy(error = "Enter a valid server, port, and key") }
            return
        }

        val server = DiscoveredServer(host.trim(), parsedPort, key.trim())
        lastServer = server
        startConnect(server)
    }

    private fun startConnect(server: DiscoveredServer) {
        val generation = ++connectGeneration
        connectJob?.cancel()
        connectJob = viewModelScope.launch { connect(server, generation) }
    }

    private suspend fun connect(server: DiscoveredServer, generation: Long) {
        _state.update { it.copy(error = null, connecting = true) }
        val result = connect(server.ip, server.port.toString(), server.secretKey)
        if (generation != connectGeneration) return
        when (result) {
            is ConnectResult.Success -> {
                prefs.savePairing(server.ip, server.port.toString(), server.secretKey, result.serverName)
                stopDiscovery()
                onSuccessCallback?.invoke()
            }
            is ConnectResult.Unauthorized ->
                _state.update { it.copy(error = "Server found but key was rejected", connecting = false) }
            is ConnectResult.NetworkError ->
                _state.update { it.copy(error = "Server found but can't connect", connecting = false) }
        }
    }

    override fun onCleared() {
        connectJob?.cancel()
        stopDiscovery()
    }
}

@OptIn(ExperimentalTvMaterial3Api::class)
@Composable
fun PairingScreen(
    apiClient: ApiClient,
    prefs: PreferencesManager,
    discovery: ServerDiscoverer,
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
    var manualEntry by remember { mutableStateOf(false) }
    var host by remember { mutableStateOf("") }
    var port by remember { mutableStateOf("8080") }
    var key by remember { mutableStateOf("") }

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

        if (state.connecting || state.error == null) {
            CircularProgressIndicator(
                modifier = Modifier.size(32.dp),
                strokeWidth = 3.dp,
                color = ReelColors.textSecondary,
            )
        }

        state.error?.let { error ->
            Spacer(modifier = Modifier.height(24.dp))
            Text(
                text = error,
                fontSize = 14.sp,
                color = ReelColors.disconnected,
            )
            Spacer(modifier = Modifier.height(16.dp))
            Row(horizontalArrangement = Arrangement.spacedBy(12.dp)) {
                Button(onClick = viewModel::retry) { Text("Retry") }
                Button(onClick = { manualEntry = !manualEntry }) { Text("Enter server") }
            }
        }

        if (manualEntry) {
            Spacer(modifier = Modifier.height(16.dp))
            Row(horizontalArrangement = Arrangement.spacedBy(12.dp)) {
                OutlinedTextField(
                    value = host,
                    onValueChange = { host = it },
                    label = { androidx.compose.material3.Text("Server address") },
                    singleLine = true,
                    modifier = Modifier.width(240.dp),
                )
                OutlinedTextField(
                    value = port,
                    onValueChange = { port = it },
                    label = { androidx.compose.material3.Text("Port") },
                    singleLine = true,
                    modifier = Modifier.width(120.dp),
                )
                OutlinedTextField(
                    value = key,
                    onValueChange = { key = it },
                    label = { androidx.compose.material3.Text("Key") },
                    singleLine = true,
                    modifier = Modifier.width(240.dp),
                )
            }
            Spacer(modifier = Modifier.height(12.dp))
            Button(onClick = { viewModel.connectManually(host, port, key) }) {
                Text("Connect")
            }
        }
    }
}
