package com.max.reel.data

import android.content.Context
import androidx.core.content.edit
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow

data class PairedServer(val host: String, val port: Int, val key: String, val name: String) {
    init {
        require(host.isNotBlank() && key.isNotBlank() && port in 1..65_535) { "Incomplete pairing" }
    }
}

// The one owner of pairing: screens route on it and every server request reads it.
class PairingStore(context: Context) {
    private val prefs = context.getSharedPreferences("reel_prefs", Context.MODE_PRIVATE)
    private val _server = MutableStateFlow(read())
    val server: StateFlow<PairedServer?> = _server.asStateFlow()

    fun save(server: PairedServer) {
        prefs.edit {
            putString("server_ip", server.host)
            putInt("server_port", server.port)
            putString("secret_key", server.key)
            putString("server_name", server.name)
        }
        _server.value = server
    }

    fun forget() {
        prefs.edit { clear() }
        _server.value = null
    }

    private fun read(): PairedServer? {
        val host = prefs.getString("server_ip", null)?.takeIf { it.isNotBlank() } ?: return null
        val key = prefs.getString("secret_key", null)?.takeIf { it.isNotBlank() } ?: return null
        val port = prefs.getInt("server_port", 0).takeIf { it in 1..65_535 } ?: return null
        return PairedServer(host, port, key, prefs.getString("server_name", null).orEmpty())
    }
}
