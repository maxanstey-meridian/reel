package com.max.reel.data

import android.content.Context

class PreferencesManager(context: Context) {

    private val prefs = context.getSharedPreferences("reel_prefs", Context.MODE_PRIVATE)

    val isPaired: Boolean
        get() = prefs.getString("reel_paired", null) == "true"

    val serverIp: String
        get() = prefs.getString("reel_server_ip", "") ?: ""

    val serverPort: String
        get() = prefs.getString("reel_server_port", "8080") ?: "8080"

    val secretKey: String
        get() = prefs.getString("reel_secret_key", "") ?: ""

    var autoAdvance: Boolean
        get() = prefs.getBoolean("reel_auto_advance", true)
        set(value) {
            prefs.edit().putBoolean("reel_auto_advance", value).apply()
        }

    fun savePairing(ip: String, port: String, secretKey: String) {
        prefs.edit()
            .putString("reel_server_ip", ip)
            .putString("reel_server_port", port)
            .putString("reel_secret_key", secretKey)
            .putString("reel_paired", "true")
            .apply()
    }

    fun clearPairing() {
        prefs.edit()
            .remove("reel_server_ip")
            .remove("reel_server_port")
            .remove("reel_secret_key")
            .putString("reel_paired", "false")
            .apply()
    }
}
