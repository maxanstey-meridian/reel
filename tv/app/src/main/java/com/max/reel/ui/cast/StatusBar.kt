package com.max.reel.ui.cast

import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.drawBehind
import androidx.compose.ui.geometry.Offset
import androidx.compose.ui.text.font.FontFamily
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.tv.material3.ExperimentalTvMaterial3Api
import androidx.tv.material3.Text
import com.max.reel.ui.theme.ReelColors
import kotlinx.coroutines.delay
import java.time.LocalDateTime
import java.time.format.DateTimeFormatter
import java.util.Locale

private val clockFormatter = DateTimeFormatter.ofPattern("EEE d MMM HH:mm", Locale.ENGLISH)

private fun currentTimeString(): String = LocalDateTime.now().format(clockFormatter)

fun formatBytes(bytes: Long): String {
    val tb = 1L shl 40
    val gb = 1L shl 30
    val mb = 1L shl 20
    val kb = 1L shl 10
    return when {
        bytes >= tb -> "%.1f TB".format(bytes.toDouble() / tb)
        bytes >= gb -> "%.1f GB".format(bytes.toDouble() / gb)
        bytes >= mb -> "%.0f MB".format(bytes.toDouble() / mb)
        bytes >= kb -> "%.0f KB".format(bytes.toDouble() / kb)
        else -> "$bytes B"
    }
}

@OptIn(ExperimentalTvMaterial3Api::class)
@Composable
fun StatusBar(
    serverAddress: String,
    userName: String?,
    totalBytes: Long?,
    modifier: Modifier = Modifier,
) {
    var clock by remember { mutableStateOf(currentTimeString()) }

    LaunchedEffect(Unit) {
        while (true) {
            delay(60_000)
            clock = currentTimeString()
        }
    }

    val borderColor = ReelColors.statusBarBorder

    Row(
        modifier = modifier
            .fillMaxWidth()
            .height(48.dp)
            .drawBehind {
                drawLine(
                    color = borderColor,
                    start = Offset(0f, 0f),
                    end = Offset(size.width, 0f),
                    strokeWidth = 1.dp.toPx(),
                )
            }
            .padding(horizontal = 24.dp),
        verticalAlignment = Alignment.CenterVertically,
        horizontalArrangement = Arrangement.SpaceBetween,
    ) {
        Text(
            text = serverAddress,
            fontSize = 14.sp,
            color = ReelColors.statusBarText,
            fontFamily = FontFamily.Monospace,
        )
        Text(
            text = userName ?: "--",
            fontSize = 14.sp,
            color = ReelColors.statusBarText,
            fontFamily = FontFamily.Monospace,
        )
        Text(
            text = totalBytes?.let { formatBytes(it) } ?: "--",
            fontSize = 14.sp,
            color = ReelColors.statusBarText,
            fontFamily = FontFamily.Monospace,
        )
        Text(
            text = clock,
            fontSize = 14.sp,
            color = ReelColors.statusBarText,
            fontFamily = FontFamily.Monospace,
        )
    }
}
