package com.max.reel.ui.cast

import android.net.Uri
import androidx.compose.animation.core.LinearEasing
import androidx.compose.animation.core.tween
import androidx.compose.foundation.background
import androidx.compose.foundation.gestures.animateScrollBy
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.aspectRatio
import androidx.compose.foundation.layout.fillMaxHeight
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.rememberLazyListState
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Brush
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.graphicsLayer
import androidx.compose.ui.graphics.painter.ColorPainter
import androidx.compose.ui.layout.ContentScale
import androidx.compose.ui.unit.dp
import coil.compose.AsyncImage
import com.max.reel.ui.theme.ReelColors

private const val REPEAT_COUNT = 100

@Composable
fun PosterMosaic(
    posterPaths: List<String>,
    serverBaseUrl: String,
    modifier: Modifier = Modifier,
) {
    if (posterPaths.isEmpty()) return

    val columns = List(3) { col ->
        posterPaths.filterIndexed { index, _ -> index % 3 == col }
    }.filter { it.isNotEmpty() }

    val listStates = List(columns.size) { rememberLazyListState() }

    // Staggered start offsets + auto-scroll per column
    columns.forEachIndexed { colIndex, columnPaths ->
        val state = listStates[colIndex]
        val virtualSize = columnPaths.size * REPEAT_COUNT
        val startOffset = when (colIndex) {
            1 -> virtualSize / 3
            2 -> (virtualSize * 2) / 3
            else -> 0
        }

        LaunchedEffect(state) {
            state.scrollToItem(startOffset)
            while (true) {
                state.animateScrollBy(
                    value = 100f,
                    animationSpec = tween(durationMillis = 3000, easing = LinearEasing),
                )
            }
        }
    }

    Box(modifier = modifier) {
        // Tilted mosaic container — scale up to compensate for rotation clipping
        Box(
            modifier = Modifier
                .fillMaxSize()
                .graphicsLayer {
                    rotationZ = -10f
                    scaleX = 1.2f
                    scaleY = 1.2f
                },
        ) {
            Row(
                modifier = Modifier.fillMaxSize(),
                horizontalArrangement = Arrangement.spacedBy(8.dp),
            ) {
                columns.forEachIndexed { colIndex, columnPaths ->
                    val virtualCount = columnPaths.size * REPEAT_COUNT

                    LazyColumn(
                        state = listStates[colIndex],
                        modifier = Modifier
                            .weight(1f)
                            .fillMaxHeight()
                            .padding(horizontal = 4.dp),
                        verticalArrangement = Arrangement.spacedBy(8.dp),
                        userScrollEnabled = false,
                    ) {
                        items(count = virtualCount) { index ->
                            val path = columnPaths[index % columnPaths.size]
                            val url = "$serverBaseUrl/api/files/thumbnail?path=${Uri.encode(path)}"

                            AsyncImage(
                                model = url,
                                contentDescription = null,
                                contentScale = ContentScale.Crop,
                                placeholder = ColorPainter(Color(0xFF222222)),
                                modifier = Modifier
                                    .fillMaxWidth()
                                    .aspectRatio(2f / 3f)
                                    .clip(RoundedCornerShape(12.dp)),
                            )
                        }
                    }
                }
            }
        }

        // Top scrim
        Box(
            modifier = Modifier
                .align(Alignment.TopCenter)
                .fillMaxWidth()
                .fillMaxHeight(0.15f)
                .background(
                    Brush.verticalGradient(
                        listOf(ReelColors.scrim, Color.Transparent),
                    ),
                ),
        )

        // Bottom scrim
        Box(
            modifier = Modifier
                .align(Alignment.BottomCenter)
                .fillMaxWidth()
                .fillMaxHeight(0.15f)
                .background(
                    Brush.verticalGradient(
                        listOf(Color.Transparent, ReelColors.scrim),
                    ),
                ),
        )

        // Left scrim
        Box(
            modifier = Modifier
                .align(Alignment.CenterStart)
                .fillMaxHeight()
                .fillMaxWidth(0.15f)
                .background(
                    Brush.horizontalGradient(
                        listOf(ReelColors.scrim, Color.Transparent),
                    ),
                ),
        )
    }
}
