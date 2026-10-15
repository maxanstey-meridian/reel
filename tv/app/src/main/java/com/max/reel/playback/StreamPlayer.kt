package com.max.reel.playback

import android.content.Context
import android.net.Uri
import androidx.annotation.OptIn
import androidx.media3.common.C
import androidx.media3.common.MediaItem
import androidx.media3.common.MimeTypes
import androidx.media3.common.PlaybackException
import androidx.media3.common.Player
import androidx.media3.common.TrackSelectionOverride
import androidx.media3.common.Tracks
import androidx.media3.common.util.UnstableApi
import androidx.media3.datasource.DefaultHttpDataSource
import androidx.media3.datasource.HttpDataSource
import androidx.media3.exoplayer.DefaultLoadControl
import androidx.media3.exoplayer.ExoPlayer
import androidx.media3.exoplayer.source.DefaultMediaSourceFactory
import com.max.reel.data.ApiClient
import com.max.reel.data.PairedServer
import com.max.reel.data.StreamInfo
import com.max.reel.data.SubtitleTrack

private const val SIDELOADED_SUBTITLE = "reel-subtitle"

// Maps the player's clock onto the file's: a transcoded stream starts at offsetSeconds and
// carries no duration of its own, while a direct stream is the file itself.
data class StreamTimeline(
    val transcodeHeight: Int? = null,
    val offsetSeconds: Double = 0.0,
    val durationSeconds: Double = 0.0,
) {
    val transcoded get() = transcodeHeight != null

    fun positionSeconds(playerPositionMs: Long): Double = offsetSeconds + playerPositionMs.coerceAtLeast(0) / 1_000.0

    fun durationSeconds(playerDurationMs: Long): Double = durationSeconds.takeIf { it > 0 }
        ?: playerDurationMs.takeIf { it > 0 && it != C.TIME_UNSET }?.div(1_000.0)
        ?: 0.0

    companion object {
        fun of(info: StreamInfo?, startSeconds: Double): StreamTimeline {
            val height = info?.transcodeHeight
            val offset = if (height != null) startSeconds.coerceAtLeast(0.0) else 0.0
            return StreamTimeline(height, offset, info?.durationSeconds ?: 0.0)
        }
    }
}

// Owns the ExoPlayer and how a Reel stream becomes a media item; it knows nothing of sessions.
@OptIn(UnstableApi::class)
class StreamPlayer(context: Context, private val api: ApiClient, private val onSubtitleUnavailable: () -> Unit) {
    private val dataSourceFactory = DefaultHttpDataSource.Factory()
    val player: ExoPlayer =
        ExoPlayer
            .Builder(context)
            .setLoadControl(
                DefaultLoadControl
                    .Builder()
                    .setBufferDurationsMs(
                        // minBufferMs =
                        15_000,
                        // maxBufferMs =
                        300_000,
                        // bufferForPlaybackMs =
                        2_500,
                        // bufferForPlaybackAfterRebufferMs =
                        5_000,
                    ).build(),
            ).setMediaSourceFactory(DefaultMediaSourceFactory(dataSourceFactory))
            .build()

    var timeline = StreamTimeline()
        private set
    private var wantsSubtitle = false
    private var embeddedSubtitle: Int? = null

    init {
        player.addListener(
            object : Player.Listener {
                override fun onTracksChanged(tracks: Tracks) = pinSubtitle(tracks)
            },
        )
    }

    val positionSeconds get() = timeline.positionSeconds(player.currentPosition)
    val durationSeconds get() = timeline.durationSeconds(player.duration)
    val error get() = player.playerError?.let(::classifyError)

    fun load(
        server: PairedServer,
        path: String,
        info: StreamInfo?,
        tracks: List<SubtitleTrack>,
        subtitle: SubtitleTrack?,
        startSeconds: Double,
    ) {
        timeline = StreamTimeline.of(info, startSeconds)
        wantsSubtitle = subtitle != null
        val embedded = tracks.filter { it.source == "embedded" }
        embeddedSubtitle =
            subtitle?.takeIf { !timeline.transcoded && it.imageBased }?.let(embedded::indexOf)
        val sideloaded =
            subtitle?.takeIf { embeddedSubtitle == null }?.let {
                MediaItem.SubtitleConfiguration
                    .Builder(
                        Uri.parse(api.subtitleUrl(server, path, it.index, timeline.offsetSeconds)),
                    ).setId(SIDELOADED_SUBTITLE)
                    .setMimeType(MimeTypes.TEXT_VTT)
                    .setSelectionFlags(C.SELECTION_FLAG_DEFAULT)
                    .build()
            }

        dataSourceFactory.setDefaultRequestProperties(api.mediaHeaders(server))
        player.setMediaItem(
            MediaItem
                .Builder()
                .setUri(
                    api.streamUrl(server, path, timeline.transcodeHeight, timeline.offsetSeconds),
                ).setSubtitleConfigurations(listOfNotNull(sideloaded))
                .build(),
            if (timeline.transcoded) 0L else (startSeconds * 1_000).toLong(),
        )
        player.trackSelectionParameters =
            player.trackSelectionParameters
                .buildUpon()
                .clearOverridesOfType(C.TRACK_TYPE_TEXT)
                .setTrackTypeDisabled(C.TRACK_TYPE_TEXT, subtitle == null)
                .build()
        player.prepare()
    }

    fun seekTo(seconds: Double) = player.seekTo(((seconds - timeline.offsetSeconds) * 1_000).toLong())

    // Embedded tracks of a direct stream can carry their own default flag, so the wanted track is
    // pinned: the sideloaded file, or an image track shown straight from the container.
    private fun pinSubtitle(tracks: Tracks) {
        if (tracks.isEmpty || !wantsSubtitle) return
        val text = tracks.groups.filter { it.type == C.TRACK_TYPE_TEXT }
        val group =
            embeddedSubtitle?.let(text::getOrNull)
                ?: text.firstOrNull {
                    it.getTrackFormat(0).id?.endsWith(SIDELOADED_SUBTITLE) == true
                }
        if (group?.isSupported != true) {
            wantsSubtitle = false
            onSubtitleUnavailable()
        } else if (!group.isSelected) {
            player.trackSelectionParameters =
                player.trackSelectionParameters
                    .buildUpon()
                    .setOverrideForType(TrackSelectionOverride(group.mediaTrackGroup, 0))
                    .build()
        }
    }
}

fun classifyError(error: PlaybackException): PlaybackError = when (val cause = error.cause) {
    is HttpDataSource.InvalidResponseCodeException -> {
        when (cause.responseCode) {
            401 -> PlaybackError("unauthorized", "Invalid server key")
            404 -> PlaybackError("file_not_found", "File not found")
            else -> PlaybackError("playback_error", "HTTP ${cause.responseCode}")
        }
    }

    is HttpDataSource.HttpDataSourceException -> {
        PlaybackError(
            "server_unreachable",
            "Can't reach server",
        )
    }

    else -> {
        PlaybackError("playback_error", error.message ?: "Playback error")
    }
}
