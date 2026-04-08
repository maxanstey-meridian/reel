package com.max.reel

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import com.max.reel.data.ApiClient
import com.max.reel.data.CastPollResult
import com.max.reel.data.PlayCommand
import kotlinx.coroutines.Job
import kotlinx.coroutines.channels.Channel
import kotlinx.coroutines.delay
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.receiveAsFlow
import kotlinx.coroutines.launch

class CastPoller(
    private val apiClient: ApiClient,
) : ViewModel() {

    private val _connectionOk = MutableStateFlow(true)
    val connectionOk: StateFlow<Boolean> = _connectionOk.asStateFlow()

    private val _playCommand = Channel<PlayCommand>(Channel.BUFFERED)
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
                _connectionOk.value = true
                val command = result.status?.command ?: return
                when (command.type) {
                    "play" -> {
                        val path = command.relativePath ?: return
                        apiClient.acknowledgeCast()
                        _playCommand.send(PlayCommand(path, command.positionSeconds))
                    }
                    "stop" -> {
                        apiClient.acknowledgeCast()
                    }
                }
            }
            is CastPollResult.Unauthorized -> {
                _connectionOk.value = false
            }
            is CastPollResult.NetworkError -> {
                _connectionOk.value = false
            }
        }
    }
}
