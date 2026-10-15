package com.max.reel.ui.theme

import androidx.compose.ui.graphics.Brush
import androidx.compose.ui.graphics.Color

object ReelColors {
    val backgroundTop = Color(0xFF0B0B1A)
    val backgroundBottom = Color(0xFF161630)
    val textPrimary = Color.White
    val textSecondary = Color(0xFFAAAAAA)
    val connected = Color(0xFF4CAF50)
    val disconnected = Color(0xFFFF9800)

    val backgroundGradient = Brush.verticalGradient(listOf(backgroundTop, backgroundBottom))
}
