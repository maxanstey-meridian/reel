package com.max.reel.data

import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test
import org.junit.runner.RunWith
import org.robolectric.RobolectricTestRunner
import org.robolectric.RuntimeEnvironment
import org.robolectric.annotation.Config

@RunWith(RobolectricTestRunner::class)
@Config(sdk = [34])
class PairingPreferencesTest {

    @Test
    fun `clearPairing removes pairing credentials`() {
        val prefs = PreferencesManager(RuntimeEnvironment.getApplication())
        prefs.savePairing("192.168.1.2", "8080", "secret", "Living Room")

        prefs.clearPairing()

        assertFalse(prefs.isPaired)
        assertEquals("", prefs.serverIp)
        assertEquals("8080", prefs.serverPort)
        assertEquals("", prefs.secretKey)
        assertEquals("", prefs.serverName)
    }

    @Test
    fun `savePairing persists server identity`() {
        val prefs = PreferencesManager(RuntimeEnvironment.getApplication())
        prefs.savePairing("192.168.1.2", "9090", "secret", "Living Room")

        assertTrue(prefs.isPaired)
        assertEquals("Living Room", prefs.serverName)
    }
}
