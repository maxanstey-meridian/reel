package com.max.reel.ui.cast

import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxHeight
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.shape.CircleShape
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
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.tv.material3.ExperimentalTvMaterial3Api
import androidx.tv.material3.Text
import com.max.reel.data.ApiClient
import com.max.reel.data.PreferencesManager
import com.max.reel.ui.theme.ReelColors

@OptIn(ExperimentalTvMaterial3Api::class)
@Composable
fun WaitingForCastScreen(
    connectionOk: Boolean,
    prefs: PreferencesManager,
    apiClient: ApiClient,
) {
    var posterPaths by remember { mutableStateOf(emptyList<String>()) }

    LaunchedEffect(Unit) {
        posterPaths = apiClient.fetchPosters(30)
    }

    val indicatorColor = if (connectionOk) ReelColors.connected else ReelColors.disconnected
    val indicatorText = if (connectionOk) "Connected" else "Waiting for cast\u2026"

    Box(
        modifier = Modifier
            .fillMaxSize()
            .background(ReelColors.backgroundGradient),
    ) {
        // Main content row — bottom padding reserves space for status bar
        Row(
            modifier = Modifier
                .fillMaxSize()
                .padding(bottom = 48.dp),
        ) {
            // Left panel — branding + connection indicator
            Column(
                modifier = Modifier
                    .weight(0.4f)
                    .fillMaxHeight()
                    .padding(horizontal = 48.dp),
                verticalArrangement = Arrangement.Center,
            ) {
                Text(
                    text = "Welcome to Reel",
                    fontSize = 42.sp,
                    fontWeight = FontWeight.Bold,
                    color = ReelColors.textPrimary,
                )

                Spacer(modifier = Modifier.height(12.dp))

                Text(
                    text = "Your media server",
                    fontSize = 18.sp,
                    color = ReelColors.textSecondary,
                )

                Spacer(modifier = Modifier.height(24.dp))

                Row(verticalAlignment = Alignment.CenterVertically) {
                    Box(
                        modifier = Modifier
                            .size(8.dp)
                            .background(color = indicatorColor, shape = CircleShape),
                    )

                    Spacer(modifier = Modifier.width(8.dp))

                    Text(
                        text = indicatorText,
                        fontSize = 14.sp,
                        color = ReelColors.textSecondary,
                    )
                }
            }

            // Right panel — poster mosaic
            PosterMosaic(
                posterPaths = posterPaths,
                serverBaseUrl = "http://${prefs.serverIp}:${prefs.serverPort}",
                modifier = Modifier
                    .weight(0.6f)
                    .fillMaxHeight(),
            )
        }

        // Status bar placeholder at bottom — thin top border, 48dp height
        val borderColor = ReelColors.statusBarBorder
        Box(
            modifier = Modifier
                .align(Alignment.BottomCenter)
                .fillMaxWidth()
                .height(48.dp)
                .drawBehind {
                    drawLine(
                        color = borderColor,
                        start = Offset(0f, 0f),
                        end = Offset(size.width, 0f),
                        strokeWidth = 1.dp.toPx(),
                    )
                },
        )
    }
}
