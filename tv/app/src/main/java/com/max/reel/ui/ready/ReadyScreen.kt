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
import androidx.tv.material3.Button
import androidx.tv.material3.Text
import com.max.reel.data.PairedServer
import com.max.reel.playback.PlaybackConnection
import com.max.reel.ui.cast.QrCodeImage
import com.max.reel.ui.theme.ReelColors

@Composable
fun ReadyScreen(
    server: PairedServer,
    pairUrl: String,
    connection: PlaybackConnection,
    connectionError: String?,
    onForgetPairing: () -> Unit,
) {
    Box(
        modifier =
        Modifier
            .fillMaxSize()
            .background(ReelColors.backgroundGradient),
    ) {
        Column(
            modifier =
            Modifier
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
                text = server.name.ifBlank { "Reel server" },
                fontSize = 18.sp,
                color = ReelColors.textPrimary,
            )
            Spacer(modifier = Modifier.height(6.dp))
            Text(
                text = "${server.host}:${server.port}",
                fontSize = 14.sp,
                color = ReelColors.textSecondary,
            )
            Spacer(modifier = Modifier.height(10.dp))
            Row(verticalAlignment = Alignment.CenterVertically) {
                Box(
                    modifier =
                    Modifier
                        .size(8.dp)
                        .background(
                            color = statusColor(connection),
                            shape = CircleShape,
                        ),
                )
                Spacer(modifier = Modifier.width(8.dp))
                Text(
                    text =
                    when (connection) {
                        PlaybackConnection.Connecting -> "Connecting…"
                        PlaybackConnection.Connected -> "Connected"
                        PlaybackConnection.Disconnected -> "Disconnected"
                    },
                    fontSize = 14.sp,
                    color =
                    if (connection == PlaybackConnection.Disconnected) {
                        ReelColors.disconnected
                    } else {
                        ReelColors.textSecondary
                    },
                )
            }
            if (connection == PlaybackConnection.Disconnected && connectionError != null) {
                Spacer(modifier = Modifier.height(6.dp))
                Text(text = connectionError, fontSize = 14.sp, color = ReelColors.disconnected)
            }
            Spacer(modifier = Modifier.height(32.dp))
            QrCodeImage(
                content = pairUrl,
                size = 160.dp,
            )
            Spacer(modifier = Modifier.height(8.dp))
            Text(
                text = "Scan to connect, or enter pairing code ${server.key}",
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

private fun statusColor(connection: PlaybackConnection) = when (connection) {
    PlaybackConnection.Connecting -> ReelColors.textSecondary
    PlaybackConnection.Connected -> ReelColors.connected
    PlaybackConnection.Disconnected -> ReelColors.disconnected
}
