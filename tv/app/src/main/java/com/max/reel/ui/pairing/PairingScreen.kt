package com.max.reel.ui.pairing

import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.widthIn
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Text as M3Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.input.KeyboardType
import androidx.compose.ui.unit.dp
import androidx.lifecycle.ViewModel
import androidx.lifecycle.ViewModelProvider
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import androidx.lifecycle.viewModelScope
import androidx.lifecycle.viewmodel.compose.viewModel
import androidx.tv.material3.Button
import androidx.tv.material3.ExperimentalTvMaterial3Api
import androidx.tv.material3.Text
import com.max.reel.data.ApiClient
import com.max.reel.data.ConnectResult
import com.max.reel.data.PreferencesManager
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.update
import kotlinx.coroutines.launch

data class PairingState(
    val ip: String = "",
    val port: String = "8080",
    val key: String = "",
    val isLoading: Boolean = false,
    val error: String? = null,
)

class PairingViewModel(
    private val apiClient: ApiClient,
    private val prefs: PreferencesManager,
) : ViewModel() {

    private val _state = MutableStateFlow(PairingState())
    val state: StateFlow<PairingState> = _state.asStateFlow()

    fun onIpChange(value: String) {
        _state.update { it.copy(ip = value, error = null) }
    }

    fun onPortChange(value: String) {
        _state.update { it.copy(port = value, error = null) }
    }

    fun onKeyChange(value: String) {
        _state.update { it.copy(key = value, error = null) }
    }

    fun onConnect(onSuccess: () -> Unit) {
        val current = _state.value
        if (current.ip.isBlank()) {
            _state.update { it.copy(error = "IP address is required") }
            return
        }
        if (current.key.isBlank()) {
            _state.update { it.copy(error = "Secret key is required") }
            return
        }

        _state.update { it.copy(isLoading = true, error = null) }

        viewModelScope.launch {
            val port = current.port.toIntOrNull() ?: 8080
            when (val result = apiClient.connect(current.ip, port, current.key)) {
                is ConnectResult.Success -> {
                    prefs.savePairing(current.ip, current.port, current.key)
                    _state.update { it.copy(isLoading = false) }
                    onSuccess()
                }
                is ConnectResult.Unauthorized -> {
                    _state.update { it.copy(isLoading = false, error = "Invalid key") }
                }
                is ConnectResult.NetworkError -> {
                    _state.update { it.copy(isLoading = false, error = "Can't reach server") }
                }
            }
        }
    }
}

@OptIn(ExperimentalTvMaterial3Api::class)
@Composable
fun PairingScreen(apiClient: ApiClient, prefs: PreferencesManager, onPaired: () -> Unit) {
    val viewModel: PairingViewModel = viewModel(
        factory = object : ViewModelProvider.Factory {
            @Suppress("UNCHECKED_CAST")
            override fun <T : ViewModel> create(modelClass: Class<T>): T {
                return PairingViewModel(apiClient, prefs) as T
            }
        }
    )

    val state by viewModel.state.collectAsStateWithLifecycle()

    Column(
        modifier = Modifier
            .fillMaxSize()
            .padding(32.dp),
        horizontalAlignment = Alignment.CenterHorizontally,
        verticalArrangement = Arrangement.Center,
    ) {
        Text(text = "Connect to Reel Server")

        Spacer(modifier = Modifier.height(24.dp))

        Column(
            modifier = Modifier.widthIn(max = 400.dp),
            verticalArrangement = Arrangement.spacedBy(12.dp),
        ) {
            OutlinedTextField(
                value = state.ip,
                onValueChange = viewModel::onIpChange,
                label = { M3Text("IP Address") },
                singleLine = true,
                modifier = Modifier.fillMaxWidth(),
                keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Uri),
            )

            OutlinedTextField(
                value = state.port,
                onValueChange = viewModel::onPortChange,
                label = { M3Text("Port") },
                singleLine = true,
                modifier = Modifier.fillMaxWidth(),
                keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Number),
            )

            OutlinedTextField(
                value = state.key,
                onValueChange = viewModel::onKeyChange,
                label = { M3Text("Secret Key") },
                singleLine = true,
                modifier = Modifier.fillMaxWidth(),
            )
        }

        Spacer(modifier = Modifier.height(16.dp))

        state.error?.let { error ->
            Text(text = error, color = Color.Red)
            Spacer(modifier = Modifier.height(8.dp))
        }

        Button(
            onClick = { viewModel.onConnect(onPaired) },
            enabled = !state.isLoading,
        ) {
            if (state.isLoading) {
                CircularProgressIndicator(
                    modifier = Modifier.size(20.dp),
                    strokeWidth = 2.dp,
                )
            } else {
                Text("Connect")
            }
        }
    }
}
