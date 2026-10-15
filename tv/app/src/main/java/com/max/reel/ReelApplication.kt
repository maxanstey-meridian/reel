package com.max.reel

import android.app.Application
import com.max.reel.data.ApiClient
import com.max.reel.data.PairingStore

// Owns the process-wide objects; lazy because preferences need the attached base context.
class ReelApplication : Application() {
    val pairing by lazy { PairingStore(this) }
    val api by lazy { ApiClient(pairing) }
}
