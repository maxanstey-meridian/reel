package com.max.reel.ui.player

import android.net.Uri
import androidx.media3.common.PlaybackException
import androidx.media3.datasource.DataSpec
import androidx.media3.datasource.HttpDataSource
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test
import org.junit.runner.RunWith
import org.robolectric.RobolectricTestRunner
import org.robolectric.annotation.Config
import java.io.IOException

@RunWith(RobolectricTestRunner::class)
@Config(sdk = [34])
class PlayerErrorTest {

    private val emptyDataSpec = DataSpec(Uri.EMPTY)

    @Test
    fun `classifyError returns Unauthorized for 401 response`() {
        val cause = HttpDataSource.InvalidResponseCodeException(
            401, "Unauthorized", null, emptyMap(), emptyDataSpec, ByteArray(0)
        )
        val error = PlaybackException("HTTP error", cause, PlaybackException.ERROR_CODE_IO_BAD_HTTP_STATUS)

        val result = classifyError(error)

        assertTrue(result is PlayerError.Unauthorized)
    }

    @Test
    fun `classifyError returns FileNotFound for 404 response`() {
        val cause = HttpDataSource.InvalidResponseCodeException(
            404, "Not Found", null, emptyMap(), emptyDataSpec, ByteArray(0)
        )
        val error = PlaybackException("HTTP error", cause, PlaybackException.ERROR_CODE_IO_BAD_HTTP_STATUS)

        val result = classifyError(error)

        assertTrue(result is PlayerError.FileNotFound)
    }

    @Test
    fun `classifyError returns Unknown with status code for other HTTP errors`() {
        val cause = HttpDataSource.InvalidResponseCodeException(
            500, "Internal Server Error", null, emptyMap(), emptyDataSpec, ByteArray(0)
        )
        val error = PlaybackException("HTTP error", cause, PlaybackException.ERROR_CODE_IO_BAD_HTTP_STATUS)

        val result = classifyError(error)

        assertTrue(result is PlayerError.Unknown)
        assertEquals("HTTP 500", (result as PlayerError.Unknown).message)
    }

    @Test
    fun `classifyError returns CannotReachServer for connection errors`() {
        val cause = HttpDataSource.HttpDataSourceException(
            IOException("Connection refused"),
            emptyDataSpec,
            PlaybackException.ERROR_CODE_IO_NETWORK_CONNECTION_FAILED,
            HttpDataSource.HttpDataSourceException.TYPE_OPEN
        )
        val error = PlaybackException("IO error", cause, PlaybackException.ERROR_CODE_IO_NETWORK_CONNECTION_FAILED)

        val result = classifyError(error)

        assertTrue(result is PlayerError.CannotReachServer)
    }

    @Test
    fun `classifyError returns Unknown for non-HTTP errors`() {
        val error = PlaybackException("Something broke", null, PlaybackException.ERROR_CODE_DECODING_FAILED)

        val result = classifyError(error)

        assertTrue(result is PlayerError.Unknown)
        assertEquals("Something broke", (result as PlayerError.Unknown).message)
    }

    @Test
    fun `classifyError returns fallback message for null message`() {
        val error = PlaybackException(null, null, PlaybackException.ERROR_CODE_UNSPECIFIED)

        val result = classifyError(error)

        assertTrue(result is PlayerError.Unknown)
        assertEquals("Playback error", (result as PlayerError.Unknown).message)
    }

    @Test
    fun `playerErrorMessage returns correct strings for all error types`() {
        assertEquals("Can't reach server", playerErrorMessage(PlayerError.CannotReachServer))
        assertEquals("Invalid key", playerErrorMessage(PlayerError.Unauthorized))
        assertEquals("File not found", playerErrorMessage(PlayerError.FileNotFound))
        assertEquals("Custom error", playerErrorMessage(PlayerError.Unknown("Custom error")))
    }
}
