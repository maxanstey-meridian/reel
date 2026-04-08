package com.max.reel.ui.cast

import androidx.compose.ui.test.junit4.createComposeRule
import org.junit.Rule
import org.junit.Test
import org.junit.runner.RunWith
import org.robolectric.RobolectricTestRunner
import org.robolectric.annotation.Config

@RunWith(RobolectricTestRunner::class)
@Config(sdk = [34])
class PosterMosaicTest {

    @get:Rule
    val composeTestRule = createComposeRule()

    @Test
    fun `empty poster list renders without crash`() {
        composeTestRule.setContent {
            PosterMosaic(
                posterPaths = emptyList(),
                serverBaseUrl = "http://localhost:8080",
            )
        }

        // PosterMosaic early-returns on empty list — verify no nodes exist
        // (the composable returns early, so the tree should be essentially empty)
        composeTestRule.waitForIdle()
        // If we got here without an exception, the empty list was handled gracefully
    }
}
