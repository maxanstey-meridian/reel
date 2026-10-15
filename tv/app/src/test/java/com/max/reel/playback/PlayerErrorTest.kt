package com.max.reel.playback

import android.net.Uri
import androidx.media3.common.PlaybackException
import androidx.media3.datasource.DataSpec
import androidx.media3.datasource.HttpDataSource
import org.junit.Assert.assertEquals
import org.junit.Test
import org.junit.runner.RunWith
import org.robolectric.RobolectricTestRunner
import org.robolectric.annotation.Config
import java.io.IOException

@RunWith(RobolectricTestRunner::class)
@Config(sdk = [34])
class PlayerErrorTest {
    private fun httpError(status: Int) = PlaybackException(
        "HTTP error",
        HttpDataSource.InvalidResponseCodeException(
            status,
            null,
            null,
            emptyMap(),
            DataSpec(Uri.EMPTY),
            ByteArray(0),
        ),
        PlaybackException.ERROR_CODE_IO_BAD_HTTP_STATUS,
    )

    @Test
    fun `HTTP statuses map to their codes`() {
        assertEquals(
            PlaybackError("unauthorized", "Invalid server key"),
            classifyError(httpError(401)),
        )
        assertEquals(
            PlaybackError("file_not_found", "File not found"),
            classifyError(httpError(404)),
        )
        assertEquals(PlaybackError("playback_error", "HTTP 500"), classifyError(httpError(500)))
    }

    @Test
    fun `connection failure is server unreachable`() {
        val cause =
            HttpDataSource.HttpDataSourceException(
                IOException("Connection refused"),
                DataSpec(Uri.EMPTY),
                PlaybackException.ERROR_CODE_IO_NETWORK_CONNECTION_FAILED,
                HttpDataSource.HttpDataSourceException.TYPE_OPEN,
            )
        val error =
            PlaybackException(
                "IO error",
                cause,
                PlaybackException.ERROR_CODE_IO_NETWORK_CONNECTION_FAILED,
            )

        assertEquals(
            PlaybackError("server_unreachable", "Can't reach server"),
            classifyError(error),
        )
    }

    @Test
    fun `other errors keep their message or fall back`() {
        val decoding =
            PlaybackException("Something broke", null, PlaybackException.ERROR_CODE_DECODING_FAILED)
        val unspecified = PlaybackException(null, null, PlaybackException.ERROR_CODE_UNSPECIFIED)

        assertEquals(PlaybackError("playback_error", "Something broke"), classifyError(decoding))
        assertEquals(PlaybackError("playback_error", "Playback error"), classifyError(unspecified))
    }
}
