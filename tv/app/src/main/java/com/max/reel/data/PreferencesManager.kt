package com.max.reel.data

import android.content.Context

class PreferencesManager(context: Context) {

    private val prefs = context.getSharedPreferences("reel_prefs", Context.MODE_PRIVATE)

    val isPaired: Boolean
        get() = prefs.getString("reel_paired", null) == "true"

    val serverIp: String
        get() = prefs.getString("reel_server_ip", "") ?: ""

    val serverPort: Int
        get() = prefs.getString("reel_server_port", "8080")?.toIntOrNull() ?: 8080

    val secretKey: String
        get() = prefs.getString("reel_secret_key", "") ?: ""

    fun savePairing(ip: String, port: String, secretKey: String) {
        prefs.edit()
            .putString("reel_server_ip", ip)
            .putString("reel_server_port", port)
            .putString("reel_secret_key", secretKey)
            .putString("reel_paired", "true")
            .apply()
    }
}
