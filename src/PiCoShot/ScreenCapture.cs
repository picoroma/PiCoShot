using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;

namespace PiCoShot
{
    /// <summary>Cattura GDI dell'intero desktop virtuale (tutti i monitor) in pixel fisici.</summary>
    internal static class ScreenCapture
    {
        public static Rectangle VirtualBounds
        {
            get { return SystemInformation.VirtualScreen; }
        }

        public static Bitmap CaptureVirtualDesktop()
        {
            Rectangle r = VirtualBounds;
            var bmp = new Bitmap(r.Width, r.Height, PixelFormat.Format32bppPArgb);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.CopyFromScreen(r.X, r.Y, 0, 0, r.Size, CopyPixelOperation.SourceCopy);
            }
            return bmp;
        }
    }
}
