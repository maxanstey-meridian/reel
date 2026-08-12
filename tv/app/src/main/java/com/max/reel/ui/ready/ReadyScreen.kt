package com.max.reel.ui.ready

import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.tv.material3.ExperimentalTvMaterial3Api
import androidx.tv.material3.Button
import androidx.tv.material3.Text
import com.max.reel.data.PreferencesManager
import com.max.reel.ui.cast.QrCodeImage
import com.max.reel.ui.theme.ReelColors

@OptIn(ExperimentalTvMaterial3Api::class)
@Composable
fun ReadyScreen(
    prefs: PreferencesManager,
    connected: Boolean = true,
    onForgetPairing: () -> Unit,
) {
    Box(
        modifier = Modifier
            .fillMaxSize()
            .background(ReelColors.backgroundGradient),
    ) {
        Column(
            modifier = Modifier
                .fillMaxSize()
                .padding(48.dp),
            horizontalAlignment = Alignment.CenterHorizontally,
            verticalArrangement = Arrangement.Center,
        ) {
            Text(
                text = "Reel is ready",
                fontSize = 42.sp,
                fontWeight = FontWeight.Bold,
                color = ReelColors.textPrimary,
            )
            Spacer(modifier = Modifier.height(12.dp))
            Text(
                text = "Waiting for a playback session",
                fontSize = 18.sp,
                color = ReelColors.textSecondary,
            )
            Spacer(modifier = Modifier.height(16.dp))
            Text(
                text = prefs.serverName.ifBlank { "Reel server" },
                fontSize = 18.sp,
                color = ReelColors.textPrimary,
            )
            Spacer(modifier = Modifier.height(6.dp))
            Text(
                text = "${prefs.serverIp}:${prefs.serverPort}",
                fontSize = 14.sp,
                color = ReelColors.textSecondary,
            )
            Spacer(modifier = Modifier.height(10.dp))
            Row(verticalAlignment = Alignment.CenterVertically) {
                Box(
                    modifier = Modifier
                        .size(8.dp)
                        .background(
                            color = if (connected) ReelColors.connected else ReelColors.disconnected,
                            shape = CircleShape,
                        ),
                )
                Spacer(modifier = Modifier.width(8.dp))
                Text(
                    text = if (connected) "Connected" else "Disconnected",
                    fontSize = 14.sp,
                    color = if (connected) ReelColors.textSecondary else ReelColors.disconnected,
                )
            }
            Spacer(modifier = Modifier.height(32.dp))
            QrCodeImage(
                content = "http://${prefs.serverIp}:${prefs.serverPort}/pair?key=${prefs.secretKey}",
                size = 160.dp,
            )
            Spacer(modifier = Modifier.height(8.dp))
            Text(
                text = "Scan to connect",
                fontSize = 13.sp,
                color = ReelColors.textSecondary,
            )
            Spacer(modifier = Modifier.height(24.dp))
            Button(onClick = onForgetPairing) {
                Text("Forget pairing")
            }
        }
    }
}
