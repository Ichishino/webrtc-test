using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Test
{
    internal class Logger
    {
        public static void Trace(string? message)
        {
            System.Diagnostics.Trace.WriteLine(message);
        }
    }

    internal class Misc
    {
        static BitmapSource ApplyContrast(BitmapSource src, double contrast)
        {
            // https://phst.hateblo.jp/entry/2018/08/24/222310

            if (contrast == 0)
            {
                return src;
            }
            
            var bitmap = new FormatConvertedBitmap(src, PixelFormats.Gray8, null, 0);
            
            int width = bitmap.PixelWidth;
            int height = bitmap.PixelHeight;
            byte[] pixcels = new byte[width * height];
            int stride = (width * bitmap.Format.BitsPerPixel + 7) / 8;

            bitmap.CopyPixels(pixcels, stride, 0);
            
            var offset = 256 * contrast;
            
            for (int x = 0; x < pixcels.Length; x++)
            {
                pixcels[x] = (byte)Math.Min(Math.Max(pixcels[x] * (1 + contrast) - offset, 0), 255);
            }
            
            return BitmapSource.Create(width, height, 96, 96, PixelFormats.Gray8, null, pixcels, stride);
        }

        static public BitmapSource ApplyEffect(BitmapSource src)
        {
            return ApplyContrast(src, 0.5);
        }
    }
}
