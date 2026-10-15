package com.max.reel.ui.cast

import androidx.compose.animation.AnimatedVisibility
import androidx.compose.animation.core.RepeatMode
import androidx.compose.animation.core.animateFloat
import androidx.compose.animation.core.infiniteRepeatable
import androidx.compose.animation.core.rememberInfiniteTransition
import androidx.compose.animation.core.tween
import androidx.compose.animation.fadeIn
import androidx.compose.animation.fadeOut
import androidx.compose.foundation.background
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
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.tv.material3.Text
import com.max.reel.ui.theme.ReelColors

@Composable
fun DisconnectedOverlay(visible: Boolean) {
    AnimatedVisibility(
        visible = visible,
        enter = fadeIn(tween(500)),
        exit = fadeOut(tween(500)),
    ) {
        Box(
            modifier =
            Modifier
                .fillMaxSize()
                .background(Color.Black.copy(alpha = 0.6f)),
            contentAlignment = Alignment.Center,
        ) {
            val pulseTransition = rememberInfiniteTransition(label = "pulse")
            val pulseAlpha by pulseTransition.animateFloat(
                initialValue = 0.4f,
                targetValue = 1.0f,
                animationSpec =
                infiniteRepeatable(
                    animation = tween(1200),
                    repeatMode = RepeatMode.Reverse,
                ),
                label = "pulseAlpha",
            )

            Column(horizontalAlignment = Alignment.CenterHorizontally) {
                Box(
                    modifier =
                    Modifier
                        .size(12.dp)
                        .alpha(pulseAlpha)
                        .background(color = ReelColors.disconnected, shape = CircleShape),
                )

                Spacer(modifier = Modifier.height(16.dp))

                Text(
                    text = "Disconnected",
                    fontSize = 28.sp,
                    color = ReelColors.disconnected,
                    fontWeight = FontWeight.SemiBold,
                )

                Spacer(modifier = Modifier.height(8.dp))

                Text(
                    text = "Can't reach the Reel server",
                    fontSize = 16.sp,
                    color = ReelColors.textSecondary,
                )
            }
        }
    }
}
