package com.max.reel.data

import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Test
import org.junit.runner.RunWith
import org.robolectric.RobolectricTestRunner
import org.robolectric.RuntimeEnvironment
import org.robolectric.annotation.Config

@RunWith(RobolectricTestRunner::class)
@Config(sdk = [34])
class PairingStoreTest {
    private val app = RuntimeEnvironment.getApplication()

    @Test
    fun `pairing survives a restart until it is forgotten`() {
        val server = PairedServer("192.168.1.2", 9090, "secret", "Living Room")
        PairingStore(app).save(server)

        val restarted = PairingStore(app)
        assertEquals(server, restarted.server.value)

        restarted.forget()
        assertNull(restarted.server.value)
        assertNull(PairingStore(app).server.value)
    }
}
