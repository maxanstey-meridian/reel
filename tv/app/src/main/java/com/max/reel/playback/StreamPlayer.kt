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
import com.max.reel.data.StreamMode
import com.max.reel.data.SubtitleTrack

private const val SIDELOADED_SUBTITLE = "reel-subtitle"

// Maps the player's clock onto the file's: a live (remux or transcode) stream starts at offsetSeconds
// and carries no duration of its own, while a direct stream is the file itself.
data class StreamTimeline(
    val mode: StreamMode = StreamMode.Direct,
    val transcodeHeight: Int? = null,
    val offsetSeconds: Double = 0.0,
    val durationSeconds: Double? = null,
) {
    val live get() = mode != StreamMode.Direct

    fun positionSeconds(playerPositionMs: Long): Double = offsetSeconds + playerPositionMs.coerceAtLeast(0) / 1_000.0

    fun durationSeconds(playerDurationMs: Long): Double = durationSeconds?.takeIf { it > 0 }
        ?: playerDurationMs.takeIf { it > 0 && it != C.TIME_UNSET }?.div(1_000.0)
        ?: 0.0

    companion object {
        fun of(info: StreamInfo?, startSeconds: Double): StreamTimeline {
            val mode = info?.mode ?: StreamMode.Direct
            val offset = if (mode != StreamMode.Direct) startSeconds.coerceAtLeast(0.0) else 0.0
            return StreamTimeline(mode, info?.transcodeHeight, offset, info?.durationSeconds)
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

    // Server subtitle indexes the player can switch to without reloading: the sideloaded files, and on a
    // direct stream the embedded tracks, read from the container in this order.
    private var sideloaded: Set<Int> = emptySet()
    private var containerTracks: List<Int> = emptyList()
    private var wantedSubtitle: Int? = null

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
        // Media3 fetches every sideloaded file as soon as it prepares, so only sidecars (a plain file read on
        // the server) are sideloaded up front. Extracting an embedded track reads the whole video, so on a
        // live stream, which carries no subtitles of its own, only the chosen one is; a direct stream shows
        // its embedded tracks straight from the container.
        containerTracks = if (timeline.live) emptyList() else tracks.filter { it.source == "embedded" }.map { it.index }
        val files =
            tracks.filter { !it.imageBased && (it.source == "external" || (timeline.live && it == subtitle)) }
        sideloaded = files.map { it.index }.toSet()
        wantedSubtitle = subtitle?.index

        dataSourceFactory.setDefaultRequestProperties(api.mediaHeaders(server))
        player.setMediaItem(
            MediaItem
                .Builder()
                .setUri(
                    api.streamUrl(server, path, timeline.mode, timeline.transcodeHeight, timeline.offsetSeconds),
                ).setSubtitleConfigurations(
                    files.map {
                        MediaItem.SubtitleConfiguration
                            .Builder(
                                Uri.parse(api.subtitleUrl(server, path, it.index, timeline.offsetSeconds)),
                            ).setId("$SIDELOADED_SUBTITLE-${it.index}")
                            .setMimeType(MimeTypes.TEXT_VTT)
                            .build()
                    },
                ).build(),
            if (timeline.live) 0L else (startSeconds * 1_000).toLong(),
        )
        hideSubtitles(disabled = subtitle == null)
        player.prepare()
    }

    /** Whether [index] (null for none) can be shown by [showSubtitle] without loading the stream again. */
    fun canSwitchTo(index: Int?): Boolean = index == null || index in sideloaded || index in containerTracks

    fun showSubtitle(index: Int?) {
        wantedSubtitle = index
        if (index == null) hideSubtitles(disabled = true) else pinSubtitle(player.currentTracks)
    }

    fun seekTo(seconds: Double) = player.seekTo(((seconds - timeline.offsetSeconds) * 1_000).toLong())

    // Embedded tracks can carry their own default flag, so the wanted track is pinned: its sideloaded
    // file, or the container track in the same position among the embedded ones.
    private fun pinSubtitle(tracks: Tracks) {
        val index = wantedSubtitle ?: return
        if (tracks.isEmpty) return
        val (files, container) =
            tracks.groups
                .filter { it.type == C.TRACK_TYPE_TEXT }
                .partition { it.getTrackFormat(0).id?.contains(SIDELOADED_SUBTITLE) == true }
        val group =
            files.firstOrNull { it.getTrackFormat(0).id?.endsWith("$SIDELOADED_SUBTITLE-$index") == true }
                ?: containerTracks.indexOf(index).takeIf { it >= 0 }?.let(container::getOrNull)
        if (group?.isSupported != true) {
            wantedSubtitle = null
            hideSubtitles(disabled = true)
            onSubtitleUnavailable()
        } else if (!group.isSelected) {
            player.trackSelectionParameters =
                player.trackSelectionParameters
                    .buildUpon()
                    .setTrackTypeDisabled(C.TRACK_TYPE_TEXT, false)
                    .setOverrideForType(TrackSelectionOverride(group.mediaTrackGroup, 0))
                    .build()
        }
    }

    private fun hideSubtitles(disabled: Boolean) {
        player.trackSelectionParameters =
            player.trackSelectionParameters
                .buildUpon()
                .clearOverridesOfType(C.TRACK_TYPE_TEXT)
                .setTrackTypeDisabled(C.TRACK_TYPE_TEXT, disabled)
                .build()
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
