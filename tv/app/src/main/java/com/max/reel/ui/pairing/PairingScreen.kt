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
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.OutlinedTextField
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import androidx.tv.material3.Button
import androidx.tv.material3.Text
import com.max.reel.data.ApiResult
import com.max.reel.data.ConnectResponse
import com.max.reel.data.DiscoveredServer
import com.max.reel.data.PairedServer
import com.max.reel.data.PairingStore
import com.max.reel.data.discoverServers
import com.max.reel.ui.theme.ReelColors
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Job
import kotlinx.coroutines.ensureActive
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.update
import kotlinx.coroutines.launch
import androidx.compose.material3.Text as FieldLabel

data class PairingState(val error: String? = null, val connecting: Boolean = false)

// Lives exactly as long as the pairing screen, so re-pairing after "Forget" starts fresh.
// Saving the pairing is what leaves this screen.
class PairingModel(
    private val scope: CoroutineScope,
    private val pairing: PairingStore,
    private val connect: suspend (DiscoveredServer) -> ApiResult<ConnectResponse>,
) {
    private val _state = MutableStateFlow(PairingState())
    val state: StateFlow<PairingState> = _state.asStateFlow()
    private var lastServer: DiscoveredServer? = null
    private var connectJob: Job? = null

    suspend fun discover(servers: Flow<DiscoveredServer>) = servers.collect { startConnect(it) }

    fun retry() {
        lastServer?.let(::startConnect)
    }

    fun connectManually(host: String, port: String, key: String) {
        val parsedPort = port.toIntOrNull()
        if (host.isBlank() || key.isBlank() || parsedPort == null || parsedPort !in 1..65535) {
            _state.update { it.copy(error = "Enter a valid server, port, and key") }
            return
        }
        startConnect(DiscoveredServer(host.trim(), parsedPort, key.trim()))
    }

    private fun startConnect(server: DiscoveredServer) {
        lastServer = server
        connectJob?.cancel()
        connectJob =
            scope.launch {
                _state.update { it.copy(error = null, connecting = true) }
                val result = connect(server)
                ensureActive()
                when (result) {
                    is ApiResult.Success -> {
                        val name = result.value.serverName.orEmpty()
                        pairing.save(PairedServer(server.host, server.port, server.key, name))
                    }

                    is ApiResult.Failure -> {
                        _state.update {
                            val error =
                                when (result.statusCode) {
                                    401 -> "Server found but key was rejected"
                                    403 -> "Server found but it only accepts devices on its own network"
                                    else -> "Server found but can't connect"
                                }
                            it.copy(error = error, connecting = false)
                        }
                    }
                }
            }
    }
}

@Composable
fun PairingScreen(pairing: PairingStore, connect: suspend (DiscoveredServer) -> ApiResult<ConnectResponse>) {
    val context = LocalContext.current
    val scope = rememberCoroutineScope()
    val model = remember { PairingModel(scope, pairing, connect) }
    val state by model.state.collectAsStateWithLifecycle()
    var manualEntry by remember { mutableStateOf(false) }
    var host by remember { mutableStateOf("") }
    var port by remember { mutableStateOf("8080") }
    var key by remember { mutableStateOf("") }

    LaunchedEffect(model) { model.discover(discoverServers(context)) }

    Column(
        modifier =
        Modifier
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
        }

        Spacer(modifier = Modifier.height(16.dp))
        Row(horizontalArrangement = Arrangement.spacedBy(12.dp)) {
            if (state.error != null) Button(onClick = model::retry) { Text("Retry") }
            Button(onClick = { manualEntry = !manualEntry }) { Text("Enter server manually") }
        }

        if (manualEntry) {
            Spacer(modifier = Modifier.height(16.dp))
            Row(horizontalArrangement = Arrangement.spacedBy(12.dp)) {
                OutlinedTextField(
                    value = host,
                    onValueChange = { host = it },
                    label = { FieldLabel("Server address") },
                    singleLine = true,
                    modifier = Modifier.width(240.dp),
                )
                OutlinedTextField(
                    value = port,
                    onValueChange = { port = it },
                    label = { FieldLabel("Port") },
                    singleLine = true,
                    modifier = Modifier.width(120.dp),
                )
                OutlinedTextField(
                    value = key,
                    onValueChange = { key = it },
                    label = { FieldLabel("Key") },
                    singleLine = true,
                    modifier = Modifier.width(240.dp),
                )
            }
            Spacer(modifier = Modifier.height(12.dp))
            Button(onClick = { model.connectManually(host, port, key) }) {
                Text("Connect")
            }
        }
    }
}
