using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace PiCoShot
{
    /// <summary>Icone vettoriali bianche (18x18 al centro del pulsante) per la toolbar.</summary>
    internal static class ToolIcons
    {
        private static Rectangle Box(Rectangle r)
        {
            return new Rectangle(r.X + (r.Width - 18) / 2, r.Y + (r.Height - 18) / 2, 18, 18);
        }

        private static Pen P(float w = 1.8f)
        {
            return new Pen(Color.White, w) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
        }

        public static void Line(Graphics g, Rectangle r)
        {
            Rectangle b = Box(r);
            using (Pen p = P(2.2f)) g.DrawLine(p, b.Left + 2, b.Bottom - 2, b.Right - 2, b.Top + 2);
        }

        public static void Arrow(Graphics g, Rectangle r)
        {
            Rectangle b = Box(r);
            using (Pen p = P(2f))
            {
                g.DrawLine(p, b.Left + 2, b.Bottom - 2, b.Right - 3, b.Top + 3);
                g.DrawLine(p, b.Right - 3, b.Top + 3, b.Right - 9, b.Top + 3);
                g.DrawLine(p, b.Right - 3, b.Top + 3, b.Right - 3, b.Top + 9);
            }
        }

        public static void Rect(Graphics g, Rectangle r)
        {
            Rectangle b = Box(r);
            using (Pen p = P(2f)) g.DrawRectangle(p, b.Left + 2, b.Top + 4, b.Width - 5, b.Height - 9);
        }

        public static void Ellipse(Graphics g, Rectangle r)
        {
            Rectangle b = Box(r);
            using (Pen p = P(2f)) g.DrawEllipse(p, b.Left + 2, b.Top + 3, b.Width - 5, b.Height - 7);
        }

        public static void Pen(Graphics g, Rectangle r)
        {
            Rectangle b = Box(r);
            using (Pen p = P(2f))
                g.DrawBezier(p, b.Left + 1, b.Bottom - 5, b.Left + 5, b.Top, b.Left + 9, b.Bottom + 2, b.Right - 1, b.Top + 5);
        }

        public static void Marker(Graphics g, Rectangle r)
        {
            Rectangle b = Box(r);
            using (var br = new SolidBrush(Color.FromArgb(235, 255, 221, 0)))
                g.FillRectangle(br, b.Left + 1, b.Top + 6, b.Width - 2, 8);
            using (Pen p = P(1.4f)) g.DrawLine(p, b.Left + 3, b.Bottom - 2, b.Right - 3, b.Bottom - 2);
        }

        public static void Text(Graphics g, Rectangle r)
        {
            Rectangle b = Box(r);
            using (var f = new Font("Georgia", 15f, FontStyle.Bold, GraphicsUnit.Pixel))
                TextRenderer.DrawText(g, "T", f, b, Color.White,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }

        /// <summary>Lettera "A" di dimensione crescente per la scelta della grandezza del testo.</summary>
        public static Action<Graphics, Rectangle> TextSize(int px)
        {
            return (g, r) =>
            {
                using (var f = new Font("Segoe UI", px, FontStyle.Bold, GraphicsUnit.Pixel))
                    TextRenderer.DrawText(g, "A", f, r, Color.White,
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            };
        }

        public static void Bold(Graphics g, Rectangle r)
        {
            using (var f = new Font("Segoe UI", 15f, FontStyle.Bold, GraphicsUnit.Pixel))
                TextRenderer.DrawText(g, "B", f, r, Color.White,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }

        public static void Italic(Graphics g, Rectangle r)
        {
            using (var f = new Font("Georgia", 15f, FontStyle.Italic | FontStyle.Bold, GraphicsUnit.Pixel))
                TextRenderer.DrawText(g, "I", f, r, Color.White,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }

        /// <summary>Testo su etichetta colorata.</summary>
        public static void Label(Graphics g, Rectangle r)
        {
            Rectangle b = Box(r);
            var box = new Rectangle(b.Left + 1, b.Top + 3, b.Width - 2, b.Height - 6);
            using (var br = new SolidBrush(Color.White)) g.FillRectangle(br, box);
            using (var f = new Font("Segoe UI", 11f, FontStyle.Bold, GraphicsUnit.Pixel))
                TextRenderer.DrawText(g, "T", f, box, Color.FromArgb(32, 32, 32),
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }

        public static void Copy(Graphics g, Rectangle r)
        {
            Rectangle b = Box(r);
            using (Pen p = P(1.6f))
            {
                g.DrawRectangle(p, b.Left + 5, b.Top + 1, 10, 12);
                g.DrawRectangle(p, b.Left + 2, b.Top + 5, 10, 12);
            }
        }

        public static void Ocr(Graphics g, Rectangle r)
        {
            using (var f = new Font("Segoe UI", 10f, FontStyle.Bold, GraphicsUnit.Pixel))
                TextRenderer.DrawText(g, "OCR", f, r, Color.White,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }

        public static void Save(Graphics g, Rectangle r)
        {
            Rectangle b = Box(r);
            using (Pen p = P(1.6f))
            {
                g.DrawRectangle(p, b.Left + 2, b.Top + 2, b.Width - 5, b.Height - 5);
                g.DrawRectangle(p, b.Left + 5, b.Top + 2, 7, 5);
                g.DrawRectangle(p, b.Left + 5, b.Top + 10, 8, 6);
            }
        }

        public static void SaveAs(Graphics g, Rectangle r)
        {
            Save(g, r);
            Rectangle b = Box(r);
            using (Pen p = P(1.8f))
            {
                g.DrawLine(p, b.Right - 1, b.Bottom - 8, b.Right - 1, b.Bottom - 1);
                g.DrawLine(p, b.Right - 4, b.Bottom - 4, b.Right + 2, b.Bottom - 4);
            }
        }

        public static void Print(Graphics g, Rectangle r)
        {
            Rectangle b = Box(r);
            using (Pen p = P(1.6f))
            {
                g.DrawRectangle(p, b.Left + 4, b.Top + 1, 10, 6);
                g.DrawRectangle(p, b.Left + 1, b.Top + 7, 16, 7);
                g.DrawRectangle(p, b.Left + 4, b.Top + 11, 10, 6);
            }
        }

        public static void Close(Graphics g, Rectangle r)
        {
            Rectangle b = Box(r);
            using (Pen p = P(2f))
            {
                g.DrawLine(p, b.Left + 3, b.Top + 3, b.Right - 3, b.Bottom - 3);
                g.DrawLine(p, b.Right - 3, b.Top + 3, b.Left + 3, b.Bottom - 3);
            }
        }
    }
}
