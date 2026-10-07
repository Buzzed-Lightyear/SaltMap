using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Xna.Framework.Graphics;

namespace SaltMap
{
    /// <summary>A decoded image, ready to upload: RGBA bytes, not premultiplied.</summary>
    internal sealed class Pixels
    {
        public int Width, Height;
        public byte[] Rgba;

        public Texture2D Upload(GraphicsDevice device)
        {
            Texture2D tex = new Texture2D(device, Width, Height, false, SurfaceFormat.Color);
            tex.SetData(Rgba);
            return tex;
        }
    }

    /// <summary>
    /// Decodes PNG and JPEG with GDI+ (System.Drawing). Safe on a worker thread, so
    /// tiles never stall the game's frame; only the upload happens on the game thread.
    /// The results suit BlendState.NonPremultiplied, which the game draws with.
    /// </summary>
    internal static class Images
    {
        public static Pixels Decode(Stream stream)
        {
            using (Bitmap bmp = new Bitmap(stream))
                return FromBitmap(bmp);
        }

        public static Pixels Decode(string path)
        {
            using (FileStream f = File.OpenRead(path))
                return Decode(f);
        }

        static Pixels FromBitmap(Bitmap bmp)
        {
            int w = bmp.Width, h = bmp.Height;
            BitmapData data = bmp.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            byte[] bytes = new byte[w * h * 4];
            try
            {
                // 32 bits per pixel, so each row is exactly w * 4 bytes unless the stride says otherwise.
                if (data.Stride == w * 4)
                    Marshal.Copy(data.Scan0, bytes, 0, bytes.Length);
                else
                    for (int y = 0; y < h; y++)
                        Marshal.Copy(data.Scan0 + y * data.Stride, bytes, y * w * 4, w * 4);
            }
            finally
            {
                bmp.UnlockBits(data);
            }
            // GDI+ gives B, G, R, A in memory; MonoGame's Color format is R, G, B, A.
            for (int i = 0; i < bytes.Length; i += 4)
            {
                byte b = bytes[i];
                bytes[i] = bytes[i + 2];
                bytes[i + 2] = b;
            }
            return new Pixels { Width = w, Height = h, Rgba = bytes };
        }
    }
}
