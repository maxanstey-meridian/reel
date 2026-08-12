package com.max.reel.ui.pairing

import android.content.Context
import com.max.reel.data.ApiClient
import com.max.reel.data.ConnectResult
import com.max.reel.data.DiscoveredServer
import com.max.reel.data.PreferencesManager
import com.max.reel.data.ServerDiscoverer
import io.ktor.client.HttpClient
import io.ktor.client.engine.mock.MockEngine
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.MutableSharedFlow
import kotlinx.coroutines.test.StandardTestDispatcher
import kotlinx.coroutines.test.advanceUntilIdle
import kotlinx.coroutines.test.resetMain
import kotlinx.coroutines.test.runTest
import kotlinx.coroutines.CompletableDeferred
import kotlinx.coroutines.NonCancellable
import kotlinx.coroutines.withContext
import kotlinx.coroutines.test.setMain
import org.junit.After
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Before
import org.junit.Test
import org.junit.runner.RunWith
import org.robolectric.RobolectricTestRunner
import org.robolectric.RuntimeEnvironment
import org.robolectric.annotation.Config

@RunWith(RobolectricTestRunner::class)
@Config(sdk = [34])
class PairingViewModelTest {

    private val dispatcher = StandardTestDispatcher()

    @Before
    fun setUp() {
        Dispatchers.setMain(dispatcher)
    }

    @After
    fun tearDown() {
        Dispatchers.resetMain()
    }

    @Test
    fun `discovery continues after failed server and stops after success`() = runTest(dispatcher) {
        val context = RuntimeEnvironment.getApplication()
        val prefs = PreferencesManager(context)
        prefs.clearPairing()
        val discovery = FakeDiscovery()
        val attempts = mutableListOf<String>()
        val apiClient = ApiClient(prefs, HttpClient(MockEngine { error("unused") }))
        val viewModel = PairingViewModel(apiClient, prefs, discovery) { host, _, _ ->
            attempts += host
            if (attempts.size == 1) ConnectResult.NetworkError("offline")
            else ConnectResult.Success("Living Room")
        }

        viewModel.startDiscovery(context)
        discovery.emit(DiscoveredServer("192.168.1.10", 8080, "bad"))
        advanceUntilIdle()
        assertEquals("Server found but can't connect", viewModel.state.value.error)

        discovery.emit(DiscoveredServer("192.168.1.11", 8080, "good"))
        advanceUntilIdle()

        assertEquals(listOf("192.168.1.10", "192.168.1.11"), attempts)
        assertTrue(prefs.isPaired)
        assertEquals("Living Room", prefs.serverName)
        assertFalse(discovery.started)
        apiClient.close()
    }

    @Test
    fun `new pairing attempt cancels and ignores stale completion`() = runTest(dispatcher) {
        val context = RuntimeEnvironment.getApplication()
        val prefs = PreferencesManager(context).also { it.clearPairing() }
        val discovery = FakeDiscovery()
        val first = CompletableDeferred<ConnectResult>()
        val apiClient = ApiClient(prefs, HttpClient(MockEngine { error("unused") }))
        val viewModel = PairingViewModel(apiClient, prefs, discovery) { host, _, _ ->
            if (host == "old") withContext(NonCancellable) { first.await() }
            else ConnectResult.Success("New server")
        }

        viewModel.connectManually("old", "8080", "key")
        dispatcher.scheduler.runCurrent()
        viewModel.connectManually("new", "8080", "key")
        dispatcher.scheduler.runCurrent()
        assertEquals("new", prefs.serverIp)
        first.complete(ConnectResult.Success("Old server"))
        advanceUntilIdle()

        assertEquals("new", prefs.serverIp)
        assertEquals("New server", prefs.serverName)
        apiClient.close()
    }

    private class FakeDiscovery : ServerDiscoverer {
        private val events = MutableSharedFlow<DiscoveredServer>()
        override val servers: Flow<DiscoveredServer> = events
        var started = false

        override fun start(context: Context) {
            started = true
        }

        override fun stop() {
            started = false
        }

        suspend fun emit(server: DiscoveredServer) {
            events.emit(server)
        }
    }
}
