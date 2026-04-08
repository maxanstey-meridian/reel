package com.max.reel.ui.cast

import androidx.compose.ui.test.assertCountEquals
import androidx.compose.ui.test.assertIsDisplayed
import androidx.compose.ui.test.junit4.createComposeRule
import androidx.compose.ui.test.onAllNodesWithText
import androidx.compose.ui.test.onNodeWithText
import androidx.tv.material3.ExperimentalTvMaterial3Api
import com.max.reel.data.ApiClient
import com.max.reel.data.PreferencesManager
import io.ktor.client.HttpClient
import io.ktor.client.engine.mock.MockEngine
import io.ktor.client.engine.mock.respond
import io.ktor.client.plugins.contentnegotiation.ContentNegotiation
import io.ktor.http.ContentType
import io.ktor.http.HttpHeaders
import io.ktor.http.HttpStatusCode
import io.ktor.http.headersOf
import io.ktor.serialization.kotlinx.json.json
import kotlinx.serialization.json.Json
import org.junit.Rule
import org.junit.Test
import org.junit.runner.RunWith
import org.robolectric.RobolectricTestRunner
import org.robolectric.RuntimeEnvironment
import org.robolectric.annotation.Config

@RunWith(RobolectricTestRunner::class)
@Config(sdk = [34])
class WaitingForCastScreenTest {

    @get:Rule
    val composeTestRule = createComposeRule()

    private val jsonHeaders = headersOf(HttpHeaders.ContentType, ContentType.Application.Json.toString())

    private fun createPrefs(): PreferencesManager {
        val context = RuntimeEnvironment.getApplication()
        val prefs = PreferencesManager(context)
        prefs.savePairing("192.168.1.1", "8080", "test-key")
        return prefs
    }

    private fun createApiClient(prefs: PreferencesManager): ApiClient {
        val mockEngine = MockEngine {
            respond("[]", HttpStatusCode.OK, jsonHeaders)
        }
        val mockClient = HttpClient(mockEngine) {
            install(ContentNegotiation) {
                json(Json { ignoreUnknownKeys = true })
            }
            expectSuccess = false
        }
        return ApiClient(prefs, mockClient)
    }

    @OptIn(ExperimentalTvMaterial3Api::class)
    @Test
    fun `shows Connected text when connectionOk is true`() {
        val prefs = createPrefs()
        val apiClient = createApiClient(prefs)

        composeTestRule.setContent {
            WaitingForCastScreen(
                connectionOk = true,
                prefs = prefs,
                apiClient = apiClient,
            )
        }

        composeTestRule.onNodeWithText("Connected").assertIsDisplayed()
        composeTestRule.onNodeWithText("Welcome to Reel").assertIsDisplayed()
        composeTestRule.onNodeWithText("Your media server").assertIsDisplayed()
    }

    @OptIn(ExperimentalTvMaterial3Api::class)
    @Test
    fun `shows Disconnected indicator text when connectionOk is false`() {
        val prefs = createPrefs()
        val apiClient = createApiClient(prefs)

        composeTestRule.setContent {
            WaitingForCastScreen(
                connectionOk = false,
                prefs = prefs,
                apiClient = apiClient,
            )
        }

        // "Disconnected" appears in both the left panel indicator and the overlay
        composeTestRule.onAllNodesWithText("Disconnected").assertCountEquals(2)
    }

    @OptIn(ExperimentalTvMaterial3Api::class)
    @Test
    fun `shows disconnected overlay with subtitle when connectionOk is false`() {
        val prefs = createPrefs()
        val apiClient = createApiClient(prefs)

        composeTestRule.setContent {
            WaitingForCastScreen(
                connectionOk = false,
                prefs = prefs,
                apiClient = apiClient,
            )
        }

        composeTestRule.onNodeWithText("Check server connection").assertIsDisplayed()
    }

    @OptIn(ExperimentalTvMaterial3Api::class)
    @Test
    fun `does not show disconnected overlay when connectionOk is true`() {
        val prefs = createPrefs()
        val apiClient = createApiClient(prefs)

        composeTestRule.setContent {
            WaitingForCastScreen(
                connectionOk = true,
                prefs = prefs,
                apiClient = apiClient,
            )
        }

        composeTestRule.onNodeWithText("Check server connection").assertDoesNotExist()
    }
}
