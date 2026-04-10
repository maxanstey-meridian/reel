package com.max.reel.ui.player

import androidx.compose.foundation.Canvas
import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.size
import androidx.compose.runtime.Composable
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.asImageBitmap
import androidx.compose.ui.platform.LocalDensity
import androidx.compose.ui.unit.IntOffset
import androidx.compose.ui.unit.IntSize
import androidx.compose.ui.unit.dp
import coil.compose.AsyncImagePainter
import coil.compose.rememberAsyncImagePainter
import com.max.reel.data.TrickplayMetadata

@Composable
fun TrickplayPreview(
    positionMs: Long,
    metadata: TrickplayMetadata,
    spriteUrlBuilder: (sheet: Int) -> String,
    modifier: Modifier = Modifier,
) {
    val thumbIndex = (positionMs / metadata.intervalMs).toInt().coerceIn(0, metadata.totalThumbs - 1)
    val thumbsPerSheet = metadata.columns * metadata.rows
    val sheetIndex = thumbIndex / thumbsPerSheet
    val subIndex = thumbIndex % thumbsPerSheet
    val row = subIndex / metadata.columns
    val col = subIndex % metadata.columns

    val density = LocalDensity.current
    val widthDp = with(density) { metadata.thumbWidth.toDp() }
    val heightDp = with(density) { metadata.thumbHeight.toDp() }

    val painter = rememberAsyncImagePainter(model = spriteUrlBuilder(sheetIndex))

    Box(modifier = modifier.size(widthDp, heightDp)) {
        when (painter.state) {
            is AsyncImagePainter.State.Success -> {
                val bitmap = (painter.state as AsyncImagePainter.State.Success)
                    .result.drawable.let { drawable ->
                        val bmp = android.graphics.Bitmap.createBitmap(
                            drawable.intrinsicWidth,
                            drawable.intrinsicHeight,
                            android.graphics.Bitmap.Config.ARGB_8888,
                        )
                        val canvas = android.graphics.Canvas(bmp)
                        drawable.setBounds(0, 0, canvas.width, canvas.height)
                        drawable.draw(canvas)
                        bmp
                    }.asImageBitmap()

                Canvas(modifier = Modifier.size(widthDp, heightDp)) {
                    drawImage(
                        image = bitmap,
                        srcOffset = IntOffset(col * metadata.thumbWidth, row * metadata.thumbHeight),
                        srcSize = IntSize(metadata.thumbWidth, metadata.thumbHeight),
                        dstOffset = IntOffset.Zero,
                        dstSize = IntSize(size.width.toInt(), size.height.toInt()),
                    )
                }
            }
            else -> {
                Box(
                    modifier = Modifier
                        .size(widthDp, heightDp)
                        .background(Color(0xFF1A1A1A)),
                )
            }
        }
    }
}
