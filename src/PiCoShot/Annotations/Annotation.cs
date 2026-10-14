using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace PiCoShot.Annotations
{
    /// <summary>
    /// Annotazione vettoriale in coordinate dell'overlay (= pixel dello schermo).
    /// Per il rendering su una bitmap ritagliata il chiamante applica una TranslateTransform.
    /// </summary>
    internal abstract class Annotation
    {
        public Color Color = Color.Red;
        public float Width = 4f;

        public abstract void Draw(Graphics g);

        /// <summary>Area da ridisegnare (include spessore e punta della freccia).</summary>
        public abstract Rectangle Bounds { get; }

        /// <summary>Trascinamento del mouse; con <paramref name="shift"/> il tratto viene vincolato.</summary>
        public virtual void Update(Point p, bool shift) { }

        /// <summary>false se l'annotazione è troppo piccola per essere tenuta (clic senza trascinamento).</summary>
        public virtual bool IsMeaningful { get { return true; } }

        protected Pen MakePen(Color color, LineCap cap)
        {
            var p = new Pen(color, Width) { LineJoin = LineJoin.Round };
            p.StartCap = cap;
            p.EndCap = cap;
            return p;
        }

        public static void Prepare(Graphics g)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        }
    }

    /// <summary>Forme definite da due punti (linea, freccia, riquadro, ellisse).</summary>
    internal abstract class ShapeAnnotation : Annotation
    {
        public Point Start, End;

        protected ShapeAnnotation(Point start) { Start = End = start; }

        public override void Update(Point p, bool shift) { End = shift ? Constrain(p) : p; }

        /// <summary>Vincolo con Maiusc: orizzontale, verticale o diagonale a 45°.</summary>
        protected virtual Point Constrain(Point p)
        {
            int dx = p.X - Start.X, dy = p.Y - Start.Y, ax = Math.Abs(dx), ay = Math.Abs(dy);
            if (ax > 2 * ay) return new Point(p.X, Start.Y);
            if (ay > 2 * ax) return new Point(Start.X, p.Y);
            int m = Math.Max(ax, ay);
            return new Point(Start.X + Math.Sign(dx) * m, Start.Y + Math.Sign(dy) * m);
        }

        public override bool IsMeaningful
        {
            get { return Math.Abs(End.X - Start.X) + Math.Abs(End.Y - Start.Y) >= 3; }
        }

        protected Rectangle Box
        {
            get { return Rectangle.FromLTRB(Math.Min(Start.X, End.X), Math.Min(Start.Y, End.Y), Math.Max(Start.X, End.X), Math.Max(Start.Y, End.Y)); }
        }

        public override Rectangle Bounds
        {
            get
            {
                Rectangle r = Box;
                int pad = (int)Width * 3 + 24;
                r.Inflate(pad, pad);
                return r;
            }
        }
    }

    internal sealed class LineAnnotation : ShapeAnnotation
    {
        public LineAnnotation(Point start) : base(start) { }

        public override void Draw(Graphics g)
        {
            using (Pen p = MakePen(Color, LineCap.Round))
                g.DrawLine(p, Start, End);
        }
    }

    internal sealed class ArrowAnnotation : ShapeAnnotation
    {
        public ArrowAnnotation(Point start) : base(start) { }

        public override void Draw(Graphics g)
        {
            double dx = End.X - Start.X, dy = End.Y - Start.Y;
            double len = Math.Sqrt(dx * dx + dy * dy);
            if (len < 1) return;
            double ux = dx / len, uy = dy / len;
            double head = Math.Min(len, Math.Max(12, Width * 4));
            double half = head * 0.5;

            var tip = new PointF(End.X, End.Y);
            var baseC = new PointF((float)(End.X - ux * head), (float)(End.Y - uy * head));
            var left = new PointF((float)(baseC.X - uy * half), (float)(baseC.Y + ux * half));
            var right = new PointF((float)(baseC.X + uy * half), (float)(baseC.Y - ux * half));

            using (Pen p = MakePen(Color, LineCap.Round))
                g.DrawLine(p, Start.X, Start.Y, (baseC.X + tip.X) / 2f, (baseC.Y + tip.Y) / 2f);
            using (var b = new SolidBrush(Color))
                g.FillPolygon(b, new[] { tip, left, right });
        }
    }

    internal sealed class RectAnnotation : ShapeAnnotation
    {
        public RectAnnotation(Point start) : base(start) { }

        public override void Draw(Graphics g)
        {
            using (Pen p = MakePen(Color, LineCap.Flat))
                g.DrawRectangle(p, Box);
        }
    }

    internal sealed class EllipseAnnotation : ShapeAnnotation
    {
        public EllipseAnnotation(Point start) : base(start) { }

        public override void Draw(Graphics g)
        {
            Rectangle r = Box;
            if (r.Width < 1 || r.Height < 1) return;
            using (Pen p = MakePen(Color, LineCap.Flat))
                g.DrawEllipse(p, r);
        }
    }

    /// <summary>Penna libera; con Maiusc il tratto diventa una retta orizzontale/verticale (sottolineatura).</summary>
    internal class PenAnnotation : Annotation
    {
        protected readonly List<Point> Points = new List<Point>();
        private bool _straight;
        private Point _straightEnd;

        public PenAnnotation(Point start) { Points.Add(start); }

        protected virtual Color StrokeColor { get { return Color; } }
        protected virtual LineCap Cap { get { return LineCap.Round; } }

        public override void Update(Point p, bool shift)
        {
            if (shift)
            {
                Point s = Points[0];
                _straight = true;
                _straightEnd = Math.Abs(p.X - s.X) >= Math.Abs(p.Y - s.Y) ? new Point(p.X, s.Y) : new Point(s.X, p.Y);
            }
            else
            {
                _straight = false;
                if (Points[Points.Count - 1] != p) Points.Add(p);
            }
        }

        private Point[] Path()
        {
            if (_straight) return new[] { Points[0], _straightEnd };
            return Points.ToArray();
        }

        public override bool IsMeaningful
        {
            get
            {
                Point[] pts = Path();
                if (pts.Length < 2) return false;
                Point f = pts[0];
                foreach (Point q in pts)
                    if (Math.Abs(q.X - f.X) + Math.Abs(q.Y - f.Y) >= 3) return true;
                return false;
            }
        }

        public override void Draw(Graphics g)
        {
            Point[] pts = Path();
            if (pts.Length == 1)
            {
                using (var b = new SolidBrush(StrokeColor))
                    g.FillEllipse(b, pts[0].X - Width / 2f, pts[0].Y - Width / 2f, Width, Width);
                return;
            }
            using (Pen p = MakePen(StrokeColor, Cap))
                g.DrawLines(p, pts);
        }

        public override Rectangle Bounds
        {
            get
            {
                Point[] pts = Path();
                int l = int.MaxValue, t = int.MaxValue, r = int.MinValue, b = int.MinValue;
                foreach (Point p in pts)
                {
                    l = Math.Min(l, p.X); t = Math.Min(t, p.Y); r = Math.Max(r, p.X); b = Math.Max(b, p.Y);
                }
                // il tratto "diritto" può essere stato disegnato in precedenza come libero: includi tutti i punti
                foreach (Point p in Points)
                {
                    l = Math.Min(l, p.X); t = Math.Min(t, p.Y); r = Math.Max(r, p.X); b = Math.Max(b, p.Y);
                }
                var rect = Rectangle.FromLTRB(l, t, r, b);
                int pad = (int)Width + 6;
                rect.Inflate(pad, pad);
                return rect;
            }
        }
    }

    /// <summary>Evidenziatore: tratto largo e semitrasparente, con estremità piatte.</summary>
    internal sealed class MarkerAnnotation : PenAnnotation
    {
        public MarkerAnnotation(Point start) : base(start) { }

        protected override Color StrokeColor { get { return System.Drawing.Color.FromArgb(105, Color); } }
        protected override LineCap Cap { get { return LineCap.Flat; } }

        /// <summary>Dritto di default (orizzontale/verticale); con Maiusc a mano libera.</summary>
        public override void Update(Point p, bool shift) { base.Update(p, !shift); }
    }

    internal sealed class TextAnnotation : Annotation
    {
        public readonly string Text;
        public readonly Point Location;
        public readonly float FontPx;
        public readonly bool Bold, Italic, Boxed;
        private readonly SizeF _size;

        /// <summary>Colore del testo: sull'etichetta (Boxed) il testo è bianco o nero a seconda dello sfondo.</summary>
        public static Color TextColorFor(Color background)
        {
            return background.GetBrightness() > 0.6f ? Color.FromArgb(24, 24, 24) : Color.White;
        }

        public static FontStyle StyleOf(bool bold, bool italic)
        {
            return (bold ? FontStyle.Bold : FontStyle.Regular) | (italic ? FontStyle.Italic : FontStyle.Regular);
        }

        public TextAnnotation(string text, Point location, float fontPx, Color color, bool bold, bool italic, bool boxed)
        {
            Text = text;
            Location = location;
            FontPx = fontPx;
            Color = color;
            Bold = bold;
            Italic = italic;
            Boxed = boxed;
            using (var bmp = new Bitmap(1, 1))
            using (Graphics g = Graphics.FromImage(bmp))
            using (Font f = MakeFont())
                _size = g.MeasureString(text, f, int.MaxValue, StringFormat.GenericTypographic);
        }

        private Font MakeFont()
        {
            return new Font("Segoe UI", FontPx, StyleOf(Bold, Italic), GraphicsUnit.Pixel);
        }

        public override void Draw(Graphics g)
        {
            using (Font f = MakeFont())
            {
                Color fore = Color;
                if (Boxed)
                {
                    var box = new RectangleF(Location.X - 6, Location.Y - 3, _size.Width + 12, _size.Height + 6);
                    using (var path = RoundRect(box, 4))
                    using (var bg = new SolidBrush(Color))
                        g.FillPath(bg, path);
                    fore = TextColorFor(Color);
                }
                using (var b = new SolidBrush(fore))
                    g.DrawString(Text, f, b, Location, StringFormat.GenericTypographic);
            }
        }

        private static GraphicsPath RoundRect(RectangleF r, float radius)
        {
            float d = radius * 2;
            var p = new GraphicsPath();
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        public override Rectangle Bounds
        {
            get
            {
                return new Rectangle(Location.X - 10, Location.Y - 8, (int)_size.Width + 20, (int)_size.Height + 16);
            }
        }
    }
}
