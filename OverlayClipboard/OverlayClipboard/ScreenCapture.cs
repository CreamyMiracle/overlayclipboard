using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OverlayClipboard
{
    public static class ScreenCapture
    {
        public static Image Capture(Rectangle screenBounds)
        {
            Rectangle bounds = new Rectangle(0, 0, screenBounds.Width, screenBounds.Height);
            Bitmap bitmap = new Bitmap(bounds.Width, bounds.Height);

            using (Graphics g = Graphics.FromImage(bitmap))
            {
                g.CopyFromScreen(screenBounds.Left, screenBounds.Top, 0, 0, bitmap.Size, CopyPixelOperation.SourceCopy);
            }
            return bitmap;
        }
    }
}
