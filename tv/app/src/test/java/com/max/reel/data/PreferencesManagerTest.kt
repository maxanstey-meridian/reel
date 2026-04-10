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
class PreferencesManagerTest {

    private fun createPrefs(): PreferencesManager =
        PreferencesManager(RuntimeEnvironment.getApplication())

    @Test
    fun `autoAdvance defaults to true on fresh prefs`() {
        val prefs = createPrefs()
        assertTrue(prefs.autoAdvance)
    }

    @Test
    fun `autoAdvance setter persists false and reads back`() {
        val prefs = createPrefs()
        prefs.autoAdvance = false
        assertFalse(prefs.autoAdvance)
    }

    @Test
    fun `autoAdvance persists across instances`() {
        val prefs1 = createPrefs()
        prefs1.autoAdvance = false

        val prefs2 = createPrefs()
        assertFalse(prefs2.autoAdvance)
    }

    @Test
    fun `autoAdvance can be toggled back to true`() {
        val prefs = createPrefs()
        prefs.autoAdvance = false
        prefs.autoAdvance = true
        assertTrue(prefs.autoAdvance)
    }
}
