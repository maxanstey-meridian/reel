@file:Suppress("ktlint:standard:filename")

package com.max.reel.data

import android.content.Context
import android.net.nsd.NsdManager
import android.net.nsd.NsdServiceInfo
import android.os.Build
import android.util.Log
import kotlinx.coroutines.channels.awaitClose
import kotlinx.coroutines.delay
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.callbackFlow
import kotlinx.coroutines.launch
import java.net.Inet4Address
import java.net.InetAddress

data class DiscoveredServer(val host: String, val port: Int, val key: String)

fun discoverServers(context: Context): Flow<DiscoveredServer> = callbackFlow {
    val nsd = context.getSystemService(NsdManager::class.java)

    fun resolve(service: NsdServiceInfo) {
        nsd.resolveService(
            service,
            object : NsdManager.ResolveListener {
                override fun onServiceResolved(info: NsdServiceInfo) {
                    val host = preferredAddress(info)?.hostAddress ?: return
                    val key = info.attributes["key"]?.decodeToString() ?: return
                    trySend(DiscoveredServer(host, info.port, key))
                }

                // Before Android 14 only one resolve may run at a time, so a second service waits its turn.
                override fun onResolveFailed(info: NsdServiceInfo, errorCode: Int) {
                    if (errorCode == NsdManager.FAILURE_ALREADY_ACTIVE) {
                        launch {
                            delay(500)
                            resolve(service)
                        }
                    } else {
                        Log.w("ServerDiscovery", "Resolving ${info.serviceName} failed: $errorCode")
                    }
                }
            },
        )
    }

    val listener =
        object : NsdManager.DiscoveryListener {
            override fun onServiceFound(service: NsdServiceInfo) = resolve(service)

            override fun onStartDiscoveryFailed(serviceType: String, errorCode: Int) {
                Log.w("ServerDiscovery", "Discovery failed to start: $errorCode")
            }

            override fun onDiscoveryStarted(serviceType: String) = Unit

            override fun onDiscoveryStopped(serviceType: String) = Unit

            override fun onStopDiscoveryFailed(serviceType: String, errorCode: Int) = Unit

            override fun onServiceLost(service: NsdServiceInfo) = Unit
        }
    nsd.discoverServices("_reel._tcp.", NsdManager.PROTOCOL_DNS_SD, listener)
    // Throws if discovery never started.
    awaitClose { runCatching { nsd.stopServiceDiscovery(listener) } }
}

// IPv4 avoids IPv6 link-local addresses, which need an interface scope to be reachable.
private fun preferredAddress(info: NsdServiceInfo): InetAddress? = if (Build.VERSION.SDK_INT >=
    Build.VERSION_CODES.UPSIDE_DOWN_CAKE
) {
    info.hostAddresses.firstOrNull { it is Inet4Address } ?: info.hostAddresses.firstOrNull()
} else {
    @Suppress("DEPRECATION")
    info.host
}
