package com.max.reel.data

import android.content.Context
import android.net.nsd.NsdManager
import android.net.nsd.NsdServiceInfo
import android.util.Log
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow

private const val TAG = "ServerDiscovery"

data class DiscoveredServer(
    val ip: String,
    val port: Int,
    val secretKey: String,
)

class ServerDiscovery {

    private val _server = MutableStateFlow<DiscoveredServer?>(null)
    val server: StateFlow<DiscoveredServer?> = _server.asStateFlow()

    private var nsdManager: NsdManager? = null
    private var discoveryListener: NsdManager.DiscoveryListener? = null

    fun start(context: Context) {
        Log.i(TAG, "Starting discovery...")
        val manager = context.getSystemService(Context.NSD_SERVICE) as NsdManager
        nsdManager = manager

        discoveryListener = object : NsdManager.DiscoveryListener {
            override fun onDiscoveryStarted(serviceType: String) {
                Log.i(TAG, "Discovery started for $serviceType")
            }
            override fun onDiscoveryStopped(serviceType: String) {
                Log.i(TAG, "Discovery stopped")
            }
            override fun onStartDiscoveryFailed(serviceType: String, errorCode: Int) {
                Log.i(TAG, "Discovery start failed: error=$errorCode")
            }
            override fun onStopDiscoveryFailed(serviceType: String, errorCode: Int) {
                Log.i(TAG, "Discovery stop failed: error=$errorCode")
            }

            override fun onServiceFound(serviceInfo: NsdServiceInfo) {
                Log.i(TAG, "Service found: ${serviceInfo.serviceName} type=${serviceInfo.serviceType}")
                manager.resolveService(serviceInfo, object : NsdManager.ResolveListener {
                    override fun onResolveFailed(serviceInfo: NsdServiceInfo, errorCode: Int) {
                        Log.i(TAG, "Resolve failed: ${serviceInfo.serviceName} error=$errorCode")
                    }

                    override fun onServiceResolved(resolved: NsdServiceInfo) {
                        val host = resolved.host?.hostAddress ?: return
                        val port = resolved.port
                        Log.i(TAG, "Resolved: $host:$port attrs=${resolved.attributes.keys}")
                        val key = resolved.attributes["key"]
                            ?.let { String(it, Charsets.UTF_8) }
                            ?: return

                        Log.i(TAG, "Server discovered: $host:$port")
                        _server.value = DiscoveredServer(host, port, key)
                    }
                })
            }

            override fun onServiceLost(serviceInfo: NsdServiceInfo) {
                Log.i(TAG, "Service lost: ${serviceInfo.serviceName}")
            }
        }

        manager.discoverServices("_reel._tcp.", NsdManager.PROTOCOL_DNS_SD, discoveryListener)
    }

    fun stop() {
        discoveryListener?.let { listener ->
            try {
                nsdManager?.stopServiceDiscovery(listener)
            } catch (_: Exception) {
                // Already stopped
            }
        }
        discoveryListener = null
        nsdManager = null
    }
}
