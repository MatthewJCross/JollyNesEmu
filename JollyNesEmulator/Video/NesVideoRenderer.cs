using JollyNesEmulator.Ppu;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace JollyNesEmulator.Video
{
    public sealed class NesVideoRenderer
    {
        private static readonly uint[] NesPalette =
        {
            0xFF666666, 0xFF002A88, 0xFF1412A7, 0xFF3B00A4, 0xFF5C007E, 0xFF6E0040, 0xFF6C0600, 0xFF561D00,
            0xFF333500, 0xFF0B4800, 0xFF005200, 0xFF004F08, 0xFF00404D, 0xFF000000, 0xFF000000, 0xFF000000,
            0xFFADADAD, 0xFF155FD9, 0xFF4240FF, 0xFF7527FE, 0xFFA01ACC, 0xFFB71E7B, 0xFFB53120, 0xFF994E00,
            0xFF6B6D00, 0xFF388700, 0xFF0C9300, 0xFF008F32, 0xFF007C8D, 0xFF000000, 0xFF000000, 0xFF000000,
            0xFFFFFEFF, 0xFF64B0FF, 0xFF9290FF, 0xFFC676FF, 0xFFF36AFF, 0xFFFE6ECC, 0xFFFE8170, 0xFFEA9E22,
            0xFFBCBE00, 0xFF88D800, 0xFF5CE430, 0xFF45E082, 0xFF48CDDE, 0xFF4F4F4F, 0xFF000000, 0xFF000000,
            0xFFFFFEFF, 0xFFC0DFFF, 0xFFD3D2FF, 0xFFE8C8FF, 0xFFFBC2FF, 0xFFFEC4EA, 0xFFFECCC5, 0xFFF7D8A5,
            0xFFE4E594, 0xFFCFEF96, 0xFFBDF4AB, 0xFFB3F3CC, 0xFFB5EBF2, 0xFFB8B8B8, 0xFF000000, 0xFF000000
        };

        private readonly byte[] _pixelBuffer = new byte[NesPpu.ScreenWidth * NesPpu.ScreenHeight * 4];

        public WriteableBitmap Bitmap { get; }

        public NesVideoRenderer()
        {
            Bitmap = new WriteableBitmap(NesPpu.ScreenWidth, NesPpu.ScreenHeight, 96, 96, PixelFormats.Bgra32, null);
        }

        public void Update(NesPpu ppu)
        {
            byte[] frameBuffer = ppu.FrameBuffer;
            byte mask = ppu.Mask;
            bool grayscale = (mask & 0x01) != 0;
            bool emphasizeRed = (mask & 0x20) != 0;
            bool emphasizeGreen = (mask & 0x40) != 0;
            bool emphasizeBlue = (mask & 0x80) != 0;

            for (int index = 0; index < frameBuffer.Length; index++)
            {
                int paletteIndex = frameBuffer[index] & 0x3F;

                if (grayscale)
                    paletteIndex &= 0x30;

                uint color = NesPalette[paletteIndex];

                if (emphasizeRed || emphasizeGreen || emphasizeBlue)
                    color = ApplyColorEmphasis(color, emphasizeRed, emphasizeGreen, emphasizeBlue);

                int pixelOffset = index * 4;

                _pixelBuffer[pixelOffset] = (byte)(color & 0xFF);
                _pixelBuffer[pixelOffset + 1] = (byte)((color >> 8) & 0xFF);
                _pixelBuffer[pixelOffset + 2] = (byte)((color >> 16) & 0xFF);
                _pixelBuffer[pixelOffset + 3] = 0xFF;
            }

            Bitmap.WritePixels(new Int32Rect(0, 0, NesPpu.ScreenWidth, NesPpu.ScreenHeight), _pixelBuffer, NesPpu.ScreenWidth * 4, 0);
        }

        private static uint ApplyColorEmphasis(uint color, bool emphasizeRed, bool emphasizeGreen, bool emphasizeBlue)
        {
            int red = (int)((color >> 16) & 0xFF);
            int green = (int)((color >> 8) & 0xFF);
            int blue = (int)(color & 0xFF);

            if (emphasizeRed)
            {
                red = Math.Min(255, (red * 110) / 100);
                green = (green * 90) / 100;
                blue = (blue * 90) / 100;
            }

            if (emphasizeGreen)
            {
                green = Math.Min(255, (green * 110) / 100);
                red = (red * 90) / 100;
                blue = (blue * 90) / 100;
            }

            if (emphasizeBlue)
            {
                blue = Math.Min(255, (blue * 110) / 100);
                red = (red * 90) / 100;
                green = (green * 90) / 100;
            }

            return 0xFF000000u | ((uint)red << 16) | ((uint)green << 8) | (uint)blue;
        }
    }
}