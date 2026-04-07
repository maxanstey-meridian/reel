package com.max.reel.ui.cast

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import com.max.reel.data.ApiClient
import com.max.reel.data.CastPollResult
import kotlinx.coroutines.Job
import kotlinx.coroutines.channels.Channel
import kotlinx.coroutines.delay
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.receiveAsFlow
import kotlinx.coroutines.launch

data class WaitingForCastState(
    val connectionOk: Boolean = true,
)

class WaitingForCastViewModel(
    private val apiClient: ApiClient,
) : ViewModel() {

    private val _state = MutableStateFlow(WaitingForCastState())
    val state: StateFlow<WaitingForCastState> = _state.asStateFlow()

    private val _playCommand = Channel<String>(Channel.BUFFERED)
    val playCommand = _playCommand.receiveAsFlow()

    private var pollingJob: Job? = null

    fun startPolling() {
        if (pollingJob?.isActive == true) return
        pollingJob = viewModelScope.launch {
            while (true) {
                poll()
                delay(2_000)
            }
        }
    }

    fun stopPolling() {
        pollingJob?.cancel()
        pollingJob = null
    }

    private suspend fun poll() {
        when (val result = apiClient.fetchCastCurrent()) {
            is CastPollResult.Success -> {
                val command = result.status?.command
                if (command == null) {
                    _state.value = WaitingForCastState(connectionOk = true)
                    return
                }
                when (command.type) {
                    "play" -> {
                        val path = command.relativePath ?: return
                        apiClient.acknowledgeCast()
                        stopPolling()
                        _playCommand.send(path)
                    }
                    "stop" -> {
                        apiClient.acknowledgeCast()
                    }
                }
                _state.value = WaitingForCastState(connectionOk = true)
            }
            is CastPollResult.Unauthorized -> {
                _state.value = WaitingForCastState(connectionOk = false)
            }
            is CastPollResult.NetworkError -> {
                _state.value = WaitingForCastState(connectionOk = false)
            }
        }
    }
}
