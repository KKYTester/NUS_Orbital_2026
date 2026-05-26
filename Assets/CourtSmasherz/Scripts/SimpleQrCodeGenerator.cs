using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace CourtSmasherz
{
    public static class SimpleQrCodeGenerator
    {
        private const int Version = 5;
        private const int Size = 17 + Version * 4;
        private const int DataCodewords = 108;
        private const int EccCodewords = 26;

        public static Texture2D Generate(string text, int pixelsPerModule)
        {
            pixelsPerModule = Mathf.Max(2, pixelsPerModule);
            bool[,] modules = new bool[Size, Size];
            bool[,] reserved = new bool[Size, Size];

            DrawFunctionPatterns(modules, reserved);
            byte[] data = MakeDataCodewords(text);
            byte[] ecc = ComputeErrorCorrection(data, EccCodewords);
            List<bool> bits = new List<bool>((data.Length + ecc.Length) * 8);
            AppendBytes(bits, data);
            AppendBytes(bits, ecc);
            DrawCodewords(modules, reserved, bits);
            DrawFormatBits(modules, reserved);

            int border = 4;
            int textureModules = Size + border * 2;
            int pixels = textureModules * pixelsPerModule;
            Texture2D texture = new Texture2D(pixels, pixels, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Point;

            for (int y = 0; y < pixels; y++)
            {
                for (int x = 0; x < pixels; x++)
                {
                    int moduleX = x / pixelsPerModule - border;
                    int moduleY = textureModules - 1 - y / pixelsPerModule - border;
                    bool black = moduleX >= 0 && moduleY >= 0 && moduleX < Size && moduleY < Size && modules[moduleX, moduleY];
                    texture.SetPixel(x, y, black ? Color.black : Color.white);
                }
            }

            texture.Apply();
            return texture;
        }

        private static byte[] MakeDataCodewords(string text)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(text ?? string.Empty);
            if (bytes.Length > 104)
            {
                byte[] trimmed = new byte[104];
                System.Array.Copy(bytes, trimmed, trimmed.Length);
                bytes = trimmed;
            }

            List<bool> bits = new List<bool>();
            AppendBits(bits, 0x4, 4);
            AppendBits(bits, bytes.Length, 8);
            foreach (byte value in bytes)
            {
                AppendBits(bits, value, 8);
            }

            int capacityBits = DataCodewords * 8;
            AppendBits(bits, 0, Mathf.Min(4, capacityBits - bits.Count));
            while (bits.Count % 8 != 0)
            {
                bits.Add(false);
            }

            List<byte> data = new List<byte>();
            for (int i = 0; i < bits.Count; i += 8)
            {
                int value = 0;
                for (int j = 0; j < 8; j++)
                {
                    value = (value << 1) | (bits[i + j] ? 1 : 0);
                }
                data.Add((byte)value);
            }

            byte pad = 0xEC;
            while (data.Count < DataCodewords)
            {
                data.Add(pad);
                pad = pad == 0xEC ? (byte)0x11 : (byte)0xEC;
            }

            return data.ToArray();
        }

        private static void DrawFunctionPatterns(bool[,] modules, bool[,] reserved)
        {
            DrawFinder(modules, reserved, 0, 0);
            DrawFinder(modules, reserved, Size - 7, 0);
            DrawFinder(modules, reserved, 0, Size - 7);

            for (int i = 8; i < Size - 8; i++)
            {
                SetFunction(modules, reserved, i, 6, i % 2 == 0);
                SetFunction(modules, reserved, 6, i, i % 2 == 0);
            }

            DrawAlignment(modules, reserved, 30, 30);
            SetFunction(modules, reserved, 8, Size - 8, true);
            ReserveFormatAreas(reserved);
        }

        private static void DrawFinder(bool[,] modules, bool[,] reserved, int left, int top)
        {
            for (int y = -1; y <= 7; y++)
            {
                for (int x = -1; x <= 7; x++)
                {
                    int xx = left + x;
                    int yy = top + y;
                    if (xx < 0 || yy < 0 || xx >= Size || yy >= Size)
                    {
                        continue;
                    }

                    bool black = (x >= 0 && x <= 6 && y >= 0 && y <= 6) &&
                        (x == 0 || x == 6 || y == 0 || y == 6 || (x >= 2 && x <= 4 && y >= 2 && y <= 4));
                    SetFunction(modules, reserved, xx, yy, black);
                }
            }
        }

        private static void DrawAlignment(bool[,] modules, bool[,] reserved, int centerX, int centerY)
        {
            for (int y = -2; y <= 2; y++)
            {
                for (int x = -2; x <= 2; x++)
                {
                    bool black = Mathf.Max(Mathf.Abs(x), Mathf.Abs(y)) != 1;
                    SetFunction(modules, reserved, centerX + x, centerY + y, black);
                }
            }
        }

        private static void ReserveFormatAreas(bool[,] reserved)
        {
            for (int i = 0; i < 9; i++)
            {
                reserved[8, i] = true;
                reserved[i, 8] = true;
                reserved[Size - 1 - i, 8] = true;
                reserved[8, Size - 1 - i] = true;
            }
        }

        private static void DrawCodewords(bool[,] modules, bool[,] reserved, List<bool> bits)
        {
            int bitIndex = 0;
            int direction = -1;
            int y = Size - 1;

            for (int right = Size - 1; right >= 1; right -= 2)
            {
                if (right == 6)
                {
                    right--;
                }

                while (true)
                {
                    for (int column = 0; column < 2; column++)
                    {
                        int x = right - column;
                        if (!reserved[x, y])
                        {
                            bool bit = bitIndex < bits.Count && bits[bitIndex++];
                            if (((x + y) & 1) == 0)
                            {
                                bit = !bit;
                            }
                            modules[x, y] = bit;
                        }
                    }

                    y += direction;
                    if (y < 0 || y >= Size)
                    {
                        y -= direction;
                        direction = -direction;
                        break;
                    }
                }
            }
        }

        private static void DrawFormatBits(bool[,] modules, bool[,] reserved)
        {
            int data = (1 << 3);
            int remainder = data;
            for (int i = 0; i < 10; i++)
            {
                remainder = (remainder << 1) ^ (((remainder >> 9) & 1) != 0 ? 0x537 : 0);
            }

            int format = ((data << 10) | (remainder & 0x3FF)) ^ 0x5412;
            for (int i = 0; i < 15; i++)
            {
                bool bit = ((format >> i) & 1) != 0;
                if (i < 6) SetFunction(modules, reserved, 8, i, bit);
                else if (i < 8) SetFunction(modules, reserved, 8, i + 1, bit);
                else SetFunction(modules, reserved, 14 - i, 8, bit);

                if (i < 8) SetFunction(modules, reserved, Size - 1 - i, 8, bit);
                else SetFunction(modules, reserved, 8, Size - 15 + i, bit);
            }
        }

        private static byte[] ComputeErrorCorrection(byte[] data, int degree)
        {
            byte[] divisor = ReedSolomonDivisor(degree);
            byte[] remainder = new byte[degree];
            foreach (byte value in data)
            {
                byte factor = (byte)(value ^ remainder[0]);
                for (int i = 0; i < degree - 1; i++)
                {
                    remainder[i] = remainder[i + 1];
                }
                remainder[degree - 1] = 0;

                for (int i = 0; i < degree; i++)
                {
                    remainder[i] ^= Multiply(divisor[i], factor);
                }
            }

            return remainder;
        }

        private static byte[] ReedSolomonDivisor(int degree)
        {
            byte[] result = new byte[degree];
            result[degree - 1] = 1;
            byte root = 1;
            for (int i = 0; i < degree; i++)
            {
                for (int j = 0; j < result.Length; j++)
                {
                    result[j] = Multiply(result[j], root);
                    if (j + 1 < result.Length)
                    {
                        result[j] ^= result[j + 1];
                    }
                }

                root = Multiply(root, 0x02);
            }

            return result;
        }

        private static byte Multiply(byte x, byte y)
        {
            int result = 0;
            int left = x;
            int right = y;
            while (right != 0)
            {
                if ((right & 1) != 0)
                {
                    result ^= left;
                }

                left <<= 1;
                if ((left & 0x100) != 0)
                {
                    left ^= 0x11D;
                }
                right >>= 1;
            }

            return (byte)result;
        }

        private static void AppendBytes(List<bool> bits, byte[] bytes)
        {
            foreach (byte value in bytes)
            {
                AppendBits(bits, value, 8);
            }
        }

        private static void AppendBits(List<bool> bits, int value, int count)
        {
            for (int i = count - 1; i >= 0; i--)
            {
                bits.Add(((value >> i) & 1) != 0);
            }
        }

        private static void SetFunction(bool[,] modules, bool[,] reserved, int x, int y, bool black)
        {
            modules[x, y] = black;
            reserved[x, y] = true;
        }
    }
}
