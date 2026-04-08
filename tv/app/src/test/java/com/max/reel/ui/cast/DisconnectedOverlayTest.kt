package com.max.reel.ui.cast

import androidx.compose.ui.test.assertIsDisplayed
import androidx.compose.ui.test.junit4.createComposeRule
import androidx.compose.ui.test.onNodeWithText
import androidx.tv.material3.ExperimentalTvMaterial3Api
import org.junit.Rule
import org.junit.Test
import org.junit.runner.RunWith
import org.robolectric.RobolectricTestRunner
import org.robolectric.annotation.Config

@RunWith(RobolectricTestRunner::class)
@Config(sdk = [34])
class DisconnectedOverlayTest {

    @get:Rule
    val composeTestRule = createComposeRule()

    @OptIn(ExperimentalTvMaterial3Api::class)
    @Test
    fun `shows Disconnected text and subtitle when visible`() {
        composeTestRule.setContent {
            DisconnectedOverlay(visible = true)
        }

        composeTestRule.onNodeWithText("Disconnected").assertIsDisplayed()
        composeTestRule.onNodeWithText("Check server connection").assertIsDisplayed()
    }

    @OptIn(ExperimentalTvMaterial3Api::class)
    @Test
    fun `hides content when not visible`() {
        composeTestRule.setContent {
            DisconnectedOverlay(visible = false)
        }

        composeTestRule.onNodeWithText("Disconnected").assertDoesNotExist()
        composeTestRule.onNodeWithText("Check server connection").assertDoesNotExist()
    }
}
