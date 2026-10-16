using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace PiCoShot
{
    /// <summary>
    /// Toolbar agganciata alla selezione. Non è una finestra: viene disegnata e gestita dall'overlay,
    /// così non ruba il focus e segue la selezione senza flicker.
    /// </summary>
    internal sealed class SelectionToolbar
    {
        public sealed class Button
        {
            public string Text;
            public string Tooltip;
            public Func<string> TooltipFunc;  // suggerimento dinamico (ha la precedenza su Tooltip)
            public Func<bool> Badge;          // se vero: contrassegno verde (azione predefinita)
            public Action Action;
            public Func<bool> IsChecked;      // pulsante a due stati (strumento attivo, colore, spessore)
            public Func<Color> Swatch;        // se presente: campione di colore al posto del testo
            public float? LineWidth;          // se presente: disegna una linea di questo spessore
            public Action<Graphics, Rectangle> Icon;   // icona vettoriale disegnata al posto del testo
            public bool Separator;
            public Func<bool> VisibleWhen;    // se presente: il pulsante c'è solo quando è vero
            public bool Shown { get { return VisibleWhen == null || VisibleWhen(); } }
            public Rectangle Bounds;
            public int Width;
        }

        private const int Pad = 4, Gap = 2, ButtonH = 26, ButtonPadX = 10, Margin = 6;

        private readonly List<Button> _buttons = new List<Button>();
        private readonly Font _font = new Font("Segoe UI", 9f);
        private int _hover = -1;
        private Rectangle _area;     // area dello schermo in cui restare (per i suggerimenti)

        public Rectangle Bounds { get; private set; }
        public bool Visible { get; private set; }

        /// <summary>Barra in colonna (strumenti a lato della selezione) invece che in riga.</summary>
        public bool Vertical { get; set; }

        private static string TipOf(Button b)
        {
            return b.TooltipFunc != null ? b.TooltipFunc() : b.Tooltip;
        }

        public Button Add(string text, string tooltip, Action action, Func<bool> isChecked = null)
        {
            var b = new Button { Text = text, Tooltip = tooltip, Action = action, IsChecked = isChecked };
            _buttons.Add(b);
            return b;
        }

        public Button AddSwatch(Func<Color> color, string tooltip, Action action, Func<bool> isChecked)
        {
            var b = new Button { Swatch = color, Tooltip = tooltip, Action = action, IsChecked = isChecked, Width = 24 };
            _buttons.Add(b);
            return b;
        }

        public Button AddLineWidth(float width, string tooltip, Action action, Func<bool> isChecked)
        {
            var b = new Button { LineWidth = width, Tooltip = tooltip, Action = action, IsChecked = isChecked, Width = 28 };
            _buttons.Add(b);
            return b;
        }

        public Button AddIcon(Action<Graphics, Rectangle> icon, string tooltip, Action action, Func<bool> isChecked = null, int width = 30)
        {
            var b = new Button { Icon = icon, Tooltip = tooltip, Action = action, IsChecked = isChecked, Width = width };
            _buttons.Add(b);
            return b;
        }

        public Button AddSeparator()
        {
            var b = new Button { Separator = true, Width = 7 };
            _buttons.Add(b);
            return b;
        }

        public string HoverTooltip
        {
            get { return _hover >= 0 ? TipOf(_buttons[_hover]) : null; }
        }

        public void Hide() { Visible = false; _hover = -1; Bounds = Rectangle.Empty; }

        private Size Measure(Graphics g)
        {
            if (Vertical)
            {
                int y = Pad;
                foreach (Button b in _buttons)
                {
                    if (!b.Shown) { b.Bounds = Rectangle.Empty; continue; }
                    b.Bounds = new Rectangle(Pad, y, 30, b.Separator ? 7 : 30);
                    y += b.Bounds.Height + Gap;
                }
                return new Size(30 + Pad * 2, y - Gap + Pad);
            }
            int x = Pad;
            foreach (Button b in _buttons)
            {
                if (!b.Shown) { b.Bounds = Rectangle.Empty; continue; }
                int w = b.Width > 0 ? b.Width : TextRenderer.MeasureText(g, b.Text, _font).Width + ButtonPadX * 2;
                b.Bounds = new Rectangle(x, Pad, w, ButtonH);
                x += w + Gap;
            }
            return new Size(x - Gap + Pad, ButtonH + Pad * 2);
        }

        private void Place(Size size, int left, int top, Rectangle workArea)
        {
            left = Math.Max(workArea.Left, Math.Min(workArea.Right - size.Width, left));
            top = Math.Max(workArea.Top, Math.Min(workArea.Bottom - size.Height, top));
            foreach (Button b in _buttons) if (b.Shown) b.Bounds.Offset(left, top);
            Bounds = new Rectangle(left, top, size.Width, size.Height);
            Visible = true;
        }

        /// <summary>
        /// Posiziona la toolbar sotto la selezione (allineata a destra); se non c'è spazio la mette sopra,
        /// altrimenti dentro la selezione. Resta sempre entro l'area dello schermo della selezione
        /// (<paramref name="workArea"/>, in coordinate client dell'overlay).
        /// </summary>
        public void Layout(Graphics g, Rectangle sel, Rectangle workArea)
        {
            _area = workArea;
            Size size = Measure(g);
            int top = sel.Bottom + Margin;
            if (top + size.Height > workArea.Bottom) top = sel.Top - size.Height - Margin;   // sopra
            if (top < workArea.Top) top = sel.Bottom - size.Height - Margin;                 // dentro, in basso
            Place(size, sel.Right - size.Width, top, workArea);
        }

        /// <summary>
        /// Barra verticale a destra della selezione (altrimenti a sinistra, altrimenti dentro a destra).
        /// <paramref name="avoid"/>: area (di un'altra barra) da non coprire.
        /// </summary>
        public void LayoutSide(Graphics g, Rectangle sel, Rectangle workArea, Rectangle avoid)
        {
            _area = workArea;
            Size size = Measure(g);
            int left = sel.Right + Margin;
            if (left + size.Width > workArea.Right) left = sel.Left - size.Width - Margin;      // a sinistra
            if (left < workArea.Left) left = sel.Right - size.Width - Margin;                   // dentro, a destra
            int top = sel.Top;
            if (!avoid.IsEmpty && new Rectangle(left, top, size.Width, size.Height).IntersectsWith(avoid))
                top = avoid.Top - size.Height - Gap - 2;                                        // sopra l'altra barra
            Place(size, left, top, workArea);
        }

        /// <summary>Posiziona una seconda barra accanto alla prima, sul lato opposto alla selezione se possibile.</summary>
        public void LayoutBeside(Graphics g, SelectionToolbar anchor, Rectangle sel, Rectangle workArea)
        {
            Size size = Measure(g);
            Rectangle a = anchor.Bounds;
            bool anchorBelowSel = a.Top >= sel.Bottom;
            int top;
            if (anchorBelowSel)
            {
                top = a.Bottom + Gap + 2;                                                   // sotto la prima barra
                if (top + size.Height > workArea.Bottom) top = sel.Top - size.Height - Margin; // sopra la selezione
            }
            else
            {
                top = a.Top - size.Height - Gap - 2;                                        // sopra la prima barra
                if (top < workArea.Top) top = sel.Bottom + Margin;                          // sotto la selezione
            }
            if (top < workArea.Top || top + size.Height > workArea.Bottom || Overlaps(top, size.Height, a))
                top = a.Top > sel.Top + size.Height + Margin ? a.Top - size.Height - Gap - 2 : a.Bottom + Gap + 2;
            Place(size, a.Right - size.Width, top, workArea);
        }

        private static bool Overlaps(int top, int height, Rectangle other)
        {
            return top < other.Bottom && top + height > other.Top;
        }

        /// <summary>Aggiorna l'hover; true se è cambiato qualcosa da ridisegnare.</summary>
        public bool MouseMove(Point p)
        {
            int h = Visible ? HitTest(p) : -1;
            if (h == _hover) return false;
            _hover = h;
            return true;
        }

        public bool Contains(Point p) { return Visible && Bounds.Contains(p); }

        /// <summary>Esegue il pulsante sotto il punto; true se il clic è stato consumato dalla toolbar.</summary>
        public bool Click(Point p)
        {
            if (!Contains(p)) return false;
            int h = HitTest(p);
            if (h >= 0 && _buttons[h].Action != null) _buttons[h].Action();
            return true;
        }

        private int HitTest(Point p)
        {
            for (int i = 0; i < _buttons.Count; i++)
                if (_buttons[i].Shown && !_buttons[i].Separator && _buttons[i].Bounds.Contains(p)) return i;
            return -1;
        }

        public void Paint(Graphics g)
        {
            if (!Visible) return;
            SmoothingMode old = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var path = RoundRect(Bounds, 6))
            using (var bg = new SolidBrush(Color.FromArgb(235, 32, 32, 32)))
            using (var edge = new Pen(Color.FromArgb(90, 255, 255, 255)))
            {
                g.FillPath(bg, path);
                g.DrawPath(edge, path);
            }
            for (int i = 0; i < _buttons.Count; i++)
            {
                Button b = _buttons[i];
                if (!b.Shown) continue;
                if (b.Separator)
                {
                    using (var sep = new Pen(Color.FromArgb(110, 255, 255, 255)))
                    {
                        if (Vertical) g.DrawLine(sep, b.Bounds.X + 4, b.Bounds.Y + b.Bounds.Height / 2, b.Bounds.Right - 4, b.Bounds.Y + b.Bounds.Height / 2);
                        else g.DrawLine(sep, b.Bounds.X + b.Bounds.Width / 2, b.Bounds.Y + 3, b.Bounds.X + b.Bounds.Width / 2, b.Bounds.Bottom - 3);
                    }
                    continue;
                }

                bool on = b.IsChecked != null && b.IsChecked();
                bool badge = b.Badge != null && b.Badge();
                if (i == _hover || on)
                    using (var path = RoundRect(b.Bounds, 4))
                    using (var hl = new SolidBrush(i == _hover ? Color.FromArgb(0, 120, 215) : Color.FromArgb(85, 85, 85)))
                        g.FillPath(hl, path);

                if (b.Swatch != null)
                {
                    var sw = new Rectangle(b.Bounds.X + 4, b.Bounds.Y + 4, b.Bounds.Width - 8, b.Bounds.Height - 8);
                    using (var fill = new SolidBrush(b.Swatch()))
                        g.FillRectangle(fill, sw);
                    using (var edge = new Pen(on ? Color.White : Color.FromArgb(150, 255, 255, 255), on ? 2 : 1))
                        g.DrawRectangle(edge, sw);
                }
                else if (b.LineWidth.HasValue)
                {
                    using (var lp = new Pen(Color.White, b.LineWidth.Value) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                        g.DrawLine(lp, b.Bounds.X + 7, b.Bounds.Y + b.Bounds.Height / 2, b.Bounds.Right - 7, b.Bounds.Y + b.Bounds.Height / 2);
                }
                else if (b.Icon != null)
                {
                    b.Icon(g, b.Bounds);
                }
                else
                {
                    TextRenderer.DrawText(g, b.Text, _font, b.Bounds, Color.White,
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
                }
            }
            for (int i = 0; i < _buttons.Count; i++)
            {
                Button b = _buttons[i];
                if (!b.Shown || b.Separator || b.Badge == null || !b.Badge()) continue;
                // azione predefinita: bordo e pallino verdi
                using (var path = RoundRect(b.Bounds, 4))
                using (var ring = new Pen(Color.FromArgb(46, 204, 113), 1.6f))
                    g.DrawPath(ring, path);
                var dot = new Rectangle(b.Bounds.Right - 9, b.Bounds.Top + 1, 8, 8);
                using (var fill = new SolidBrush(Color.FromArgb(46, 204, 113)))
                using (var edge = new Pen(Color.FromArgb(32, 32, 32), 1.5f))
                {
                    g.FillEllipse(fill, dot);
                    g.DrawEllipse(edge, dot);
                }
            }
            PaintTooltip(g);
            g.SmoothingMode = old;
        }

        /// <summary>Area (oltre alla barra) in cui può comparire il suggerimento: da ridisegnare al cambio di hover.</summary>
        public Rectangle TooltipArea
        {
            get { return Bounds.IsEmpty ? Bounds : (Vertical ? Rectangle.Inflate(Bounds, 230, 10) : Rectangle.Inflate(Bounds, 160, 34)); }
        }

        private void PaintTooltip(Graphics g)
        {
            if (_hover < 0 || string.IsNullOrEmpty(TipOf(_buttons[_hover]))) return;
            Button b = _buttons[_hover];
            string tip = TipOf(b);
            Size ts = TextRenderer.MeasureText(g, tip, _font);
            Rectangle box;
            if (Vertical)
            {
                // colonna: suggerimento di lato (a sinistra della barra, altrimenti a destra)
                box = new Rectangle(Bounds.Left - ts.Width - 16, b.Bounds.Y + (b.Bounds.Height - ts.Height - 6) / 2, ts.Width + 12, ts.Height + 6);
                if (box.Left < _area.Left + 2) box.X = Bounds.Right + 4;
            }
            else
            {
                box = new Rectangle(b.Bounds.X + b.Bounds.Width / 2 - (ts.Width + 12) / 2, Bounds.Bottom + 4, ts.Width + 12, ts.Height + 6);
                if (box.Bottom > _area.Bottom) box.Y = Bounds.Top - box.Height - 4;
                box.X = Math.Max(_area.Left + 2, Math.Min(_area.Right - box.Width - 2, box.X));
            }
            using (var path = RoundRect(box, 4))
            using (var bg = new SolidBrush(Color.FromArgb(240, 20, 20, 20)))
            using (var edge = new Pen(Color.FromArgb(90, 255, 255, 255)))
            {
                g.FillPath(bg, path);
                g.DrawPath(edge, path);
            }
            TextRenderer.DrawText(g, tip, _font, box, Color.White,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }

        private static GraphicsPath RoundRect(Rectangle r, int radius)
        {
            int d = radius * 2;
            var p = new GraphicsPath();
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d - 1, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d - 1, r.Bottom - d - 1, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d - 1, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }
    }
}
