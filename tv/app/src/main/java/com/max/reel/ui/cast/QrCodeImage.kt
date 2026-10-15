package com.max.reel.ui.cast

import android.graphics.Bitmap
import android.graphics.Color
import androidx.compose.foundation.Image
import androidx.compose.foundation.layout.size
import androidx.compose.runtime.Composable
import androidx.compose.runtime.remember
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.asImageBitmap
import androidx.compose.ui.unit.Dp
import com.google.zxing.BarcodeFormat
import com.google.zxing.EncodeHintType
import com.google.zxing.qrcode.QRCodeWriter

@Composable
fun QrCodeImage(content: String, size: Dp, modifier: Modifier = Modifier) {
    val bitmap =
        remember(content) {
            val matrix =
                QRCodeWriter().encode(
                    content,
                    BarcodeFormat.QR_CODE,
                    512,
                    512,
                    mapOf(EncodeHintType.MARGIN to 1),
                )
            val pixels =
                IntArray(matrix.width * matrix.height) { i ->
                    if (matrix[i % matrix.width, i / matrix.width]) Color.WHITE else Color.TRANSPARENT
                }
            Bitmap
                .createBitmap(
                    pixels,
                    matrix.width,
                    matrix.height,
                    Bitmap.Config.ARGB_8888,
                ).asImageBitmap()
        }

    Image(
        bitmap = bitmap,
        contentDescription = "QR code to connect",
        modifier = modifier.size(size),
    )
}
