package com.max.reel.ui.cast

import androidx.compose.animation.core.RepeatMode
import androidx.compose.animation.core.animateFloat
import androidx.compose.animation.core.infiniteRepeatable
import androidx.compose.animation.core.rememberInfiniteTransition
import androidx.compose.animation.core.tween
import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.alpha
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.tv.material3.ExperimentalTvMaterial3Api
import androidx.tv.material3.Text
import com.max.reel.data.PreferencesManager

@OptIn(ExperimentalTvMaterial3Api::class)
@Composable
fun WaitingForCastScreen(
    connectionOk: Boolean,
    prefs: PreferencesManager,
) {
    val infiniteTransition = rememberInfiniteTransition(label = "pulse")
    val pulseAlpha by infiniteTransition.animateFloat(
        initialValue = 0.4f,
        targetValue = 1.0f,
        animationSpec = infiniteRepeatable(
            animation = tween(durationMillis = 1_200),
            repeatMode = RepeatMode.Reverse,
        ),
        label = "pulseAlpha",
    )

    Column(
        modifier = Modifier.fillMaxSize(),
        horizontalAlignment = Alignment.CenterHorizontally,
        verticalArrangement = Arrangement.Center,
    ) {
        Box(
            modifier = Modifier
                .size(16.dp)
                .alpha(pulseAlpha)
                .background(
                    color = if (connectionOk) Color(0xFF4CAF50) else Color(0xFFF44336),
                    shape = CircleShape,
                ),
        )

        Spacer(modifier = Modifier.height(24.dp))

        Text(
            text = "Waiting for cast\u2026",
            fontSize = 24.sp,
            color = Color.White,
        )

        Spacer(modifier = Modifier.height(12.dp))

        Text(
            text = "${prefs.serverIp}:${prefs.serverPort}",
            fontSize = 14.sp,
            color = Color.Gray,
        )
    }
}
