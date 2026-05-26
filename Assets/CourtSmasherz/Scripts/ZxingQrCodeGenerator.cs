using UnityEngine;
using ZXing;
using ZXing.Common;
using ZXing.QrCode;

/*
    For local QR code generation independent of external API
*/

namespace CourtSmasherz
{
    public static class ZxingQrCodeGenerator
    {
        public static Texture2D Generate(string text, int size = 512)
        {
            if (string.IsNullOrEmpty(text))
            {
                Debug.LogWarning("Cannot generate QR code because text is empty.");
                return null;
            }

            var writer = new BarcodeWriterPixelData
            {
                Format = BarcodeFormat.QR_CODE,
                Options = new QrCodeEncodingOptions
                {
                    Width = size,
                    Height = size,
                    Margin = 4,
                    CharacterSet = "UTF-8"
                }
            };

            var pixelData = writer.Write(text);

            Texture2D texture = new Texture2D(pixelData.Width, pixelData.Height);
            Color32[] pixels = new Color32[pixelData.Width * pixelData.Height];

            for (int i = 0; i < pixels.Length; i++)
            {
                int byteIndex = i * 4;

                byte r = pixelData.Pixels[byteIndex];
                byte g = pixelData.Pixels[byteIndex + 1];
                byte b = pixelData.Pixels[byteIndex + 2];
                byte a = pixelData.Pixels[byteIndex + 3];

                pixels[i] = new Color32(r, g, b, a);
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;

            return texture;
        }
    }
}