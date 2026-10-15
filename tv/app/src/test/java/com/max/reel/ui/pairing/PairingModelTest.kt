package com.max.reel.ui.pairing

import com.max.reel.data.ApiResult
import com.max.reel.data.ConnectResponse
import com.max.reel.data.DiscoveredServer
import com.max.reel.data.PairingStore
import kotlinx.coroutines.CompletableDeferred
import kotlinx.coroutines.NonCancellable
import kotlinx.coroutines.flow.MutableSharedFlow
import kotlinx.coroutines.launch
import kotlinx.coroutines.test.runCurrent
import kotlinx.coroutines.test.runTest
import kotlinx.coroutines.withContext
import org.junit.Assert.assertEquals
import org.junit.Test
import org.junit.runner.RunWith
import org.robolectric.RobolectricTestRunner
import org.robolectric.RuntimeEnvironment
import org.robolectric.annotation.Config

@RunWith(RobolectricTestRunner::class)
@Config(sdk = [34])
class PairingModelTest {
    private val pairing = PairingStore(RuntimeEnvironment.getApplication()).also { it.forget() }

    @Test
    fun `discovery continues after a rejected server and pairs with the next`() = runTest {
        val servers = MutableSharedFlow<DiscoveredServer>()
        val attempts = mutableListOf<String>()
        val model =
            PairingModel(backgroundScope, pairing) {
                attempts += it.host
                if (attempts.size == 1) {
                    ApiResult.Failure("Unauthorized", 401)
                } else {
                    ApiResult.Success(ConnectResponse("Living Room"))
                }
            }
        backgroundScope.launch { model.discover(servers) }
        runCurrent()

        servers.emit(DiscoveredServer("192.168.1.10", 8080, "bad"))
        runCurrent()
        assertEquals("Server found but key was rejected", model.state.value.error)

        servers.emit(DiscoveredServer("192.168.1.11", 8080, "good"))
        runCurrent()
        assertEquals(listOf("192.168.1.10", "192.168.1.11"), attempts)
        assertEquals("Living Room", pairing.server.value?.name)
    }

    @Test
    fun `new pairing attempt cancels and ignores stale completion`() = runTest {
        val first = CompletableDeferred<ApiResult<ConnectResponse>>()
        val model =
            PairingModel(backgroundScope, pairing) {
                if (it.host == "old") {
                    withContext(NonCancellable) { first.await() }
                } else {
                    ApiResult.Success(ConnectResponse("New server"))
                }
            }

        model.connectManually("old", "8080", "key")
        runCurrent()
        model.connectManually("new", "8080", "key")
        runCurrent()
        first.complete(ApiResult.Success(ConnectResponse("Old server")))
        runCurrent()

        assertEquals("new", pairing.server.value?.host)
        assertEquals("New server", pairing.server.value?.name)
    }
}
