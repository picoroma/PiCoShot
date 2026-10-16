using System;
using System.Collections.Generic;
using System.Drawing.Printing;
using PiCoShot.Annotations;
using System.IO;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace PiCoShot
{
    /// <summary>
    /// Form fullscreen senza bordi che copre il desktop virtuale. Viene istanziato una sola volta
    /// all'avvio e riutilizzato: a ogni cattura riceve la bitmap e diventa visibile.
    /// Le coordinate client coincidono con i pixel fisici dello screenshot (nessuno scaling DPI).
    /// </summary>
    internal sealed class CaptureOverlay : Form
    {
        private const int WM_DPICHANGED = 0x02E0;
        private const int HandleSize = 8;
        private const int MinSize = 2;

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr after, int x, int y, int cx, int cy, uint flags);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        private const uint SWP_SHOWWINDOW = 0x0040;
        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);

        private enum Drag { None, Create, Move, Resize, Draw, MoveText }
        private enum Tool { None, Line, Arrow, Rect, Ellipse, Pen, Marker, Text }

        private static readonly Color[] Palette =
        {
            Color.FromArgb(255, 0, 0), Color.FromArgb(255, 140, 0), Color.FromArgb(255, 221, 0),
            Color.FromArgb(0, 176, 80), Color.FromArgb(0, 112, 240), Color.FromArgb(0, 0, 0)
        };
        private static readonly float[] PenWidths = { 2f, 4f, 7f };
        private static readonly float[] MarkerWidths = { 12f, 18f, 26f };
        private static readonly float[] TextSizes = { 14f, 20f, 28f, 40f };

        private Tool _tool = Tool.None;
        private Color _color = Palette[0];
        private bool _colorChosen;                 // true dopo una scelta esplicita del colore
        private int _thick = 1;                    // indice in PenWidths/MarkerWidths
        private int _textSize = 1;                 // indice in TextSizes
        private bool _bold, _italic, _boxed;       // stile del testo
        private readonly List<Annotation> _annotations = new List<Annotation>();
        private Annotation _current;               // annotazione in corso di disegno
        private TextBox _textBox;                  // editor del testo, presente solo durante l'inserimento
        private Point _textDragFrom, _textBoxFrom; // trascinamento del riquadro di testo (mouse e posizione iniziali)
        private const int TextFrameGap = 7;        // spessore della cornice afferrabile attorno al testo

        /// <summary>Cornice tratteggiata attorno al riquadro di testo: trascinandola si sposta il testo.</summary>
        private Rectangle TextFrame
        {
            get { return _textBox == null ? Rectangle.Empty : Rectangle.Inflate(_textBox.Bounds, TextFrameGap, TextFrameGap); }
        }

        private bool OnTextFrame(Point p)
        {
            return _textBox != null && TextFrame.Contains(p) && !_textBox.Bounds.Contains(p);
        }

        private Bitmap _shot;      // screenshot originale
        private Bitmap _dimmed;    // screenshot oscurato al 40%
        private Rectangle _sel;    // selezione corrente (coordinate client), vuota se assente
        private Drag _drag;
        private int _handle;       // indice maniglia durante Resize
        private Point _anchor;     // punto di inizio trascinamento
        private Rectangle _selAtDragStart;
        private Point _cursor;
        private readonly SelectionToolbar _toolbar = new SelectionToolbar();   // barra sotto la selezione: colori, opzioni, azioni, OK/Annulla
        private readonly SelectionToolbar _tools = new SelectionToolbar { Vertical = true };   // strumenti, in colonna a lato

        /// <summary>Messaggio da mostrare all'utente dopo la chiusura dell'overlay (titolo, testo, icona).</summary>
        public event Action<string, string, ToolTipIcon> Notify;

        public CaptureOverlay()
        {
            BuildBar();

            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = false;
            TopMost = true;
            AutoScaleMode = AutoScaleMode.None;
            Cursor = Cursors.Cross;
            BackColor = Color.Black;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
        }

        public bool IsActive { get { return Visible; } }

        /// <summary>Mostra l'overlay sullo screenshot appena catturato; la bitmap passa in proprietà all'overlay.</summary>
        public void ShowCapture(Bitmap shot, Rectangle preselect = default(Rectangle))
        {
            DisposeBitmaps();
            _shot = shot;
            _dimmed = BuildDimmed(shot);
            _sel = Rectangle.Empty;
            _drag = Drag.None;
            ResetAnnotations();
            // colore predefinito dalle impostazioni; se diverso dal rosso vale come scelta esplicita (anche per l'evidenziatore)
            _color = Settings.DefaultColor;
            _colorChosen = _color.ToArgb() != Palette[0].ToArgb();
            Cursor = Cursors.Cross;

            if (!Visible) Show();
            ApplyBounds();
            Activate();
            SetForegroundWindow(Handle);
            _cursor = PointToClient(Cursor.Position);
            if (!preselect.IsEmpty)
            {
                // cattura schermo intero: l'area parte già selezionata (modificabile, annotabile, Invio la copia)
                _sel = preselect;
                UpdateToolbar();
            }
            Invalidate();
        }

        private void ApplyBounds()
        {
            Rectangle v = ScreenCapture.VirtualBounds;
            SetWindowPos(Handle, HWND_TOPMOST, v.X, v.Y, v.Width, v.Height, SWP_SHOWWINDOW);
        }

        private static Bitmap BuildDimmed(Bitmap src)
        {
            var d = new Bitmap(src.Width, src.Height, PixelFormat.Format32bppPArgb);
            using (Graphics g = Graphics.FromImage(d))
            using (var b = new SolidBrush(Color.FromArgb(102, 0, 0, 0))) // 40%
            {
                g.CompositingMode = CompositingMode.SourceCopy;
                g.DrawImageUnscaled(src, 0, 0);
                g.CompositingMode = CompositingMode.SourceOver;
                g.FillRectangle(b, 0, 0, d.Width, d.Height);
            }
            return d;
        }

        private void CloseOverlay()
        {
            Hide();
            _drag = Drag.None;
            DiscardText();
            _toolbar.Hide();
            _tools.Hide();
            ResetAnnotations();
            DisposeBitmaps();
        }

        private void ResetAnnotations()
        {
            _annotations.Clear();
            _current = null;
            _tool = Tool.None;
        }

        /// <summary>Ricalcola posizione/visibilità delle due barre e ridisegna le zone interessate.</summary>
        private void UpdateToolbar()
        {
            Rectangle before1 = _toolbar.Bounds, before2 = _tools.Bounds;
            bool show = !_sel.IsEmpty && _sel.Width >= MinSize && _sel.Height >= MinSize
                        && (_drag == Drag.None || _drag == Drag.Draw);
            if (show)
            {
                using (Graphics g = CreateGraphics())
                {
                    Rectangle wa = WorkAreaOf(_sel);
                    _toolbar.Layout(g, _sel, wa);                       // sotto (o sopra/dentro)
                    _tools.LayoutSide(g, _sel, wa, _toolbar.Bounds);    // a destra (o a sinistra/dentro)
                }
            }
            else
            {
                _toolbar.Hide();
                _tools.Hide();
            }
            InvalidateBar(before1, _toolbar.Bounds);
            InvalidateBar(before2, _tools.Bounds);
        }

        private void InvalidateBar(Rectangle before, Rectangle after)
        {
            if (before == after) return;
            if (!before.IsEmpty) Invalidate(Rectangle.Inflate(before, 2, 2));
            if (!after.IsEmpty) Invalidate(Rectangle.Inflate(after, 2, 2));
        }

        private bool OverBar(Point p) { return _toolbar.Contains(p) || _tools.Contains(p); }

        // ---------- strumenti di annotazione ----------

        private void BuildBar()
        {
            // colonna a lato: strumenti di annotazione
            AddToolButton(ToolIcons.Line, "Linea / sottolineatura (Maiusc = dritta)", Tool.Line);
            AddToolButton(ToolIcons.Arrow, "Freccia", Tool.Arrow);
            AddToolButton(ToolIcons.Rect, "Riquadro (Maiusc = quadrato)", Tool.Rect);
            AddToolButton(ToolIcons.Ellipse, "Cerchio / ellisse (Maiusc = cerchio)", Tool.Ellipse);
            AddToolButton(ToolIcons.Pen, "Penna libera (Maiusc = dritta)", Tool.Pen);
            AddToolButton(ToolIcons.Marker, "Evidenziatore dritto (Maiusc = a mano libera)", Tool.Marker);
            AddToolButton(ToolIcons.Text, "Testo (Invio conferma, Maiusc+Invio a capo)", Tool.Text);

            // barra sotto: colori | spessore o opzioni testo | azioni | OK / Annulla
            foreach (Color c in Palette)
            {
                Color captured = c;
                _toolbar.AddSwatch(() => captured, "Colore", () => SetColor(captured), () => _color.ToArgb() == captured.ToArgb());
            }
            _toolbar.AddSwatch(() => IsPaletteColor(_color) ? Color.FromArgb(120, 120, 120) : _color, "Altro colore...",
                PickCustomColor, () => !IsPaletteColor(_color));
            _toolbar.AddSeparator();
            for (int i = 0; i < PenWidths.Length; i++)
            {
                int idx = i;
                _toolbar.AddLineWidth(PenWidths[i], "Spessore", () => { _thick = idx; RefreshTools(); }, () => _thick == idx)
                    .VisibleWhen = () => _tool != Tool.Text;
            }
            // opzioni del testo: compaiono al posto degli spessori quando lo strumento Testo è attivo
            Func<bool> textOn = () => _tool == Tool.Text;
            string[] sizeNames = { "Testo piccolo", "Testo medio", "Testo grande", "Testo molto grande" };
            int[] glyph = { 9, 12, 15, 18 };
            for (int i = 0; i < TextSizes.Length; i++)
            {
                int idx = i;
                _toolbar.AddIcon(ToolIcons.TextSize(glyph[i]), sizeNames[i], () => { _textSize = idx; RefreshTools(); }, () => _textSize == idx, 26)
                    .VisibleWhen = textOn;
            }
            _toolbar.AddSeparator().VisibleWhen = textOn;
            _toolbar.AddIcon(ToolIcons.Bold, "Grassetto", () => { _bold = !_bold; RefreshTools(); }, () => _bold, 28).VisibleWhen = textOn;
            _toolbar.AddIcon(ToolIcons.Italic, "Corsivo", () => { _italic = !_italic; RefreshTools(); }, () => _italic, 28).VisibleWhen = textOn;
            _toolbar.AddIcon(ToolIcons.Label, "Etichetta: testo su sfondo colorato", () => { _boxed = !_boxed; RefreshTools(); }, () => _boxed, 28).VisibleWhen = textOn;
            _toolbar.AddSeparator();

            // azioni singole; quelle predefinite hanno il contrassegno verde
            _toolbar.AddIcon(ToolIcons.Copy, "Copia (Ctrl+C)", CopySelection).Badge = () => IsDefault(DefaultAction.Copy);
            _toolbar.AddIcon(ToolIcons.Ocr, "Copia testo - OCR (Ctrl+T)", CopyText, null, 38).Badge = () => IsDefault(DefaultAction.Ocr);
            _toolbar.AddIcon(ToolIcons.Save, "Salva (Ctrl+S)", SaveDirect).Badge = () => IsDefault(DefaultAction.Save);
            _toolbar.AddIcon(ToolIcons.SaveAs, "Salva con nome (Ctrl+Shift+S)", SaveAs);
            _toolbar.AddIcon(ToolIcons.Print, "Stampa (Ctrl+P)", PrintSelection);
            _toolbar.AddSeparator();

            // OK = azione predefinita, Annulla = chiude senza fare nulla
            var ok = _toolbar.AddIcon(ToolIcons.Ok, null, RunDefaultActions, null, 40);
            ok.TooltipFunc = () => "OK (Invio): " + DefaultActionsText();
            _toolbar.AddIcon(ToolIcons.Cancel, "Annulla tutto (Esc)", CloseOverlay, null, 40);
        }

        private static bool IsDefault(DefaultAction a)
        {
            return (Settings.DefaultActions & a) != 0;
        }

        private static string DefaultActionsText()
        {
            var parts = new System.Collections.Generic.List<string>();
            if (IsDefault(DefaultAction.Copy)) parts.Add("copia");
            if (IsDefault(DefaultAction.Save)) parts.Add("salva");
            if (IsDefault(DefaultAction.Ocr)) parts.Add("OCR su file");
            return parts.Count == 0 ? "copia" : string.Join(" + ", parts.ToArray());
        }

        private void AddToolButton(Action<Graphics, Rectangle> icon, string tip, Tool tool)
        {
            _tools.AddIcon(icon, tip, () => SelectTool(tool), () => _tool == tool);
        }

        private static bool IsPaletteColor(Color c)
        {
            foreach (Color p in Palette) if (p.ToArgb() == c.ToArgb()) return true;
            return false;
        }

        private void SelectTool(Tool tool)
        {
            CommitText();
            _tool = _tool == tool ? Tool.None : tool;
            // finché l'utente non sceglie un colore: giallo per l'evidenziatore, rosso per gli altri
            if (!_colorChosen) _color = _tool == Tool.Marker ? Palette[2] : Palette[0];
            UpdateToolbar();
            RefreshTools();
            UpdateCursor(PointToClient(Cursor.Position));
        }

        private void SetColor(Color c)
        {
            _color = c;
            _colorChosen = true;
            RefreshTools();
        }

        private void PickCustomColor()
        {
            CommitText();
            using (var dlg = new ColorDialog { Color = _color, FullOpen = true })
            {
                if (dlg.ShowDialog(this) == DialogResult.OK) SetColor(dlg.Color);
            }
        }

        private void RefreshTools()
        {
            if (_toolbar.Visible) Invalidate(Rectangle.Inflate(_toolbar.Bounds, 2, 2));
            if (_tools.Visible) Invalidate(Rectangle.Inflate(_tools.Bounds, 2, 2));
        }

        private float CurrentWidth(Tool tool)
        {
            return tool == Tool.Marker ? MarkerWidths[_thick] : PenWidths[_thick];
        }

        private Annotation CreateAnnotation(Point p)
        {
            Annotation a;
            switch (_tool)
            {
                case Tool.Line: a = new LineAnnotation(p); break;
                case Tool.Arrow: a = new ArrowAnnotation(p); break;
                case Tool.Rect: a = new RectAnnotation(p); break;
                case Tool.Ellipse: a = new EllipseAnnotation(p); break;
                case Tool.Pen: a = new PenAnnotation(p); break;
                case Tool.Marker: a = new MarkerAnnotation(p); break;
                default: return null;
            }
            a.Color = _color;
            a.Width = CurrentWidth(_tool);
            return a;
        }

        private Point ClampToSel(Point p)
        {
            return new Point(Math.Max(_sel.Left, Math.Min(_sel.Right - 1, p.X)), Math.Max(_sel.Top, Math.Min(_sel.Bottom - 1, p.Y)));
        }

        private bool CanDrawAt(Point p)
        {
            return _tool != Tool.None && !_sel.IsEmpty && _sel.Contains(p) && HitHandle(p) < 0 && !OverBar(p);
        }

        // ---------- testo ----------

        private void BeginText(Point p)
        {
            DiscardText();
            float px = TextSizes[_textSize];
            _textBox = new TextBox
            {
                Multiline = true,
                AcceptsReturn = true,
                WordWrap = false,
                BorderStyle = BorderStyle.None,
                ScrollBars = ScrollBars.None,
                BackColor = _boxed ? _color : (_color.GetBrightness() > 0.55f ? Color.FromArgb(45, 45, 45) : Color.FromArgb(255, 255, 235)),
                ForeColor = _boxed ? TextAnnotation.TextColorFor(_color) : _color,
                Font = new Font("Segoe UI", px, TextAnnotation.StyleOf(_bold, _italic), GraphicsUnit.Pixel),
                Location = p,
                Text = ""
            };
            _textBox.TextChanged += (s, e) => FitTextBox();
            FitTextBox();
            Controls.Add(_textBox);
            _textBox.Focus();
            Cursor = Cursors.IBeam;
        }

        private void FitTextBox()
        {
            if (_textBox == null) return;
            Rectangle oldFrame = TextFrame;
            Size s = TextRenderer.MeasureText(_textBox.Text + "W", _textBox.Font, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.NoPadding);
            int lines = Math.Max(1, _textBox.Text.Split('\n').Length);
            _textBox.Size = new Size(Math.Max(90, s.Width + 8), Math.Max(_textBox.Font.Height, lines * _textBox.Font.Height) + 4);
            Invalidate(Rectangle.Inflate(Rectangle.Union(oldFrame, TextFrame), 2, 2));
        }

        /// <summary>Conferma il testo digitato trasformandolo in annotazione.</summary>
        private void CommitText()
        {
            if (_textBox == null) return;
            string text = _textBox.Text.TrimEnd('\r', '\n', ' ');
            var a = !string.IsNullOrWhiteSpace(text)
                ? new TextAnnotation(text, new Point(_textBox.Left + 1, _textBox.Top + 1), _textBox.Font.Size, _boxed ? _textBox.BackColor : _textBox.ForeColor, _bold, _italic, _boxed)
                : null;
            DiscardText();
            if (a != null)
            {
                _annotations.Add(a);
                Invalidate(a.Bounds);
            }
            Focus();
        }

        private void DiscardText()
        {
            if (_textBox == null) return;
            Rectangle r = TextFrame;
            Controls.Remove(_textBox);
            _textBox.Dispose();
            _textBox = null;
            if (Visible) Invalidate(Rectangle.Inflate(r, 2, 2));
        }

        private void Undo()
        {
            if (_annotations.Count == 0) return;
            Annotation last = _annotations[_annotations.Count - 1];
            _annotations.RemoveAt(_annotations.Count - 1);
            Invalidate(last.Bounds);
        }

        /// <summary>Area dello schermo che contiene la selezione, in coordinate client (l'overlay copre anche la taskbar).</summary>
        private Rectangle WorkAreaOf(Rectangle sel)
        {
            Rectangle v = ScreenCapture.VirtualBounds;
            var screenRect = new Rectangle(sel.X + v.X, sel.Y + v.Y, sel.Width, sel.Height);
            Rectangle wa = Screen.FromRectangle(screenRect).Bounds;
            wa.Offset(-v.X, -v.Y);
            return wa;
        }

        private void DisposeBitmaps()
        {
            if (_shot != null) { _shot.Dispose(); _shot = null; }
            if (_dimmed != null) { _dimmed.Dispose(); _dimmed = null; }
        }

        // ---------- azioni ----------

        private void CopySelection()
        {
            if (_sel.Width < MinSize || _sel.Height < MinSize || _shot == null) return;
            try
            {
                using (Bitmap crop = Render(_sel))
                {
                    Clipboard.SetImage(crop);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex);
            }
            CloseOverlay();
        }

        private bool _ocrBusy;

        /// <summary>Ctrl+T: OCR sull'immagine originale (senza annotazioni), testo negli appunti, toast con le prime righe.</summary>
        private async void CopyText()
        {
            if (!HasSelection || _ocrBusy) return;
            _ocrBusy = true;
            Cursor = Cursors.WaitCursor;
            string title = "PiCoShot", message = null;
            ToolTipIcon icon = ToolTipIcon.Info;
            Bitmap crop = Crop(_sel);
            try
            {
                string text = await OcrService.RecognizeAsync(crop);
                if (string.IsNullOrWhiteSpace(text))
                {
                    message = "Nessun testo riconosciuto nell'area selezionata.";
                    icon = ToolTipIcon.Warning;
                }
                else
                {
                    Clipboard.SetText(text);
                    message = "Testo copiato:\n" + Preview(text);
                }
            }
            catch (OcrUnavailableException ex)
            {
                message = ex.Message;
                icon = ToolTipIcon.Warning;
            }
            catch (Exception ex)
            {
                Log.Error(ex);
                message = "OCR non riuscito: " + ex.Message;
                icon = ToolTipIcon.Error;
            }
            finally
            {
                crop.Dispose();
                _ocrBusy = false;
            }
            CloseOverlay();
            Report(title, message, icon);
        }

        /// <summary>
        /// Pulsante OK (✔) / Invio: esegue le azioni predefinite delle Impostazioni (anche tutte insieme):
        /// copia immagine, salva PNG, OCR accodato a PiCoShot_OCR.txt (testo anche negli appunti).
        /// Con copia + OCR gli appunti contengono immagine e testo insieme.
        /// </summary>
        private async void RunDefaultActions()
        {
            if (!HasSelection || _ocrBusy) return;
            _ocrBusy = true;
            Cursor = Cursors.WaitCursor;
            DefaultAction acts = Settings.DefaultActions == DefaultAction.None ? DefaultAction.Copy : Settings.DefaultActions;
            var lines = new System.Collections.Generic.List<string>();
            ToolTipIcon icon = ToolTipIcon.Info;
            Bitmap image = null, original = null;
            try
            {
                if ((acts & (DefaultAction.Copy | DefaultAction.Save)) != 0) image = Render(_sel);   // con annotazioni

                if ((acts & DefaultAction.Save) != 0)
                    lines.Add("Salvato: " + Output.SaveImage(image));

                string text = null;
                if ((acts & DefaultAction.Ocr) != 0)
                {
                    original = Crop(_sel);   // l'OCR lavora sull'immagine originale, senza annotazioni
                    try
                    {
                        text = await OcrService.RecognizeAsync(original);
                    }
                    catch (OcrUnavailableException ex)
                    {
                        lines.Add(ex.Message);
                        icon = ToolTipIcon.Warning;
                    }
                    if (string.IsNullOrWhiteSpace(text))
                    {
                        text = null;
                        if (icon == ToolTipIcon.Info) { lines.Add("OCR: nessun testo riconosciuto."); icon = ToolTipIcon.Warning; }
                    }
                    else
                    {
                        string file = Output.AppendOcr(text);
                        lines.Add("OCR accodato a: " + file);
                        lines.Add(Preview(text));
                    }
                }

                bool copyImage = (acts & DefaultAction.Copy) != 0;
                if (copyImage || text != null)
                {
                    var data = new DataObject();
                    if (copyImage) data.SetImage(image);
                    if (text != null) data.SetText(text);
                    Clipboard.SetDataObject(data, true);
                    lines.Insert(0, "Negli appunti: " + (copyImage && text != null ? "immagine e testo" : copyImage ? "immagine" : "testo"));
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex);
                lines.Add("Errore: " + ex.Message);
                icon = ToolTipIcon.Error;
            }
            finally
            {
                if (image != null) image.Dispose();
                if (original != null) original.Dispose();
                _ocrBusy = false;
            }
            CloseOverlay();
            string msg = string.Join("\n", lines.ToArray());
            if (msg.Length > 250) msg = msg.Substring(0, 247) + "...";   // limite del fumetto di Windows
            Report("PiCoShot", msg, icon);
        }

        private static string Preview(string text)
        {
            string[] lines = text.Replace("\r", "").Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
            string p = string.Join("\n", lines, 0, Math.Min(3, lines.Length));
            if (p.Length > 180) p = p.Substring(0, 180) + "…";
            return lines.Length > 3 ? p + "\n…" : p;
        }

        private bool HasSelection
        {
            get { return _shot != null && _sel.Width >= MinSize && _sel.Height >= MinSize; }
        }

        private void Report(string title, string text, ToolTipIcon icon)
        {
            var h = Notify;
            if (h != null) h(title, text, icon);
        }

        /// <summary>Ctrl+S: salva il PNG nella cartella delle Impostazioni, senza finestre di dialogo.</summary>
        private void SaveDirect()
        {
            if (!HasSelection) return;
            try
            {
                string folder = Settings.ResolvedSaveFolder;
                Directory.CreateDirectory(folder);
                string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string path = Path.Combine(folder, "PiCoShot_" + stamp + ".png");
                for (int n = 2; File.Exists(path); n++)
                    path = Path.Combine(folder, "PiCoShot_" + stamp + "_" + n + ".png");

                using (Bitmap crop = Render(_sel))
                    crop.Save(path, ImageFormat.Png);
                CloseOverlay();
                Report("PiCoShot", "Salvato: " + path, ToolTipIcon.Info);
            }
            catch (Exception ex)
            {
                Log.Error(ex);
                CloseOverlay();
                Report("PiCoShot", "Salvataggio non riuscito: " + ex.Message, ToolTipIcon.Error);
            }
        }

        /// <summary>Ctrl+Shift+S: "Salva con nome" (PNG, JPG, BMP). Annullando si resta nell'overlay.</summary>
        private void SaveAs()
        {
            if (!HasSelection) return;
            try
            {
                string folder = Settings.ResolvedSaveFolder;
                Directory.CreateDirectory(folder);
                using (var dlg = new SaveFileDialog())
                {
                    dlg.Title = "Salva con nome";
                    dlg.InitialDirectory = folder;
                    dlg.FileName = "PiCoShot_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");
                    dlg.Filter = "PNG (*.png)|*.png|JPEG (*.jpg)|*.jpg|Bitmap (*.bmp)|*.bmp";
                    dlg.DefaultExt = "png";
                    dlg.AddExtension = true;
                    dlg.OverwritePrompt = true;
                    if (dlg.ShowDialog(this) != DialogResult.OK) return;

                    ImageFormat fmt = ImageFormat.Png;
                    string ext = Path.GetExtension(dlg.FileName).ToLowerInvariant();
                    if (ext == ".jpg" || ext == ".jpeg") fmt = ImageFormat.Jpeg;
                    else if (ext == ".bmp") fmt = ImageFormat.Bmp;

                    using (Bitmap crop = Render(_sel))
                        crop.Save(dlg.FileName, fmt);
                    string saved = dlg.FileName;
                    CloseOverlay();
                    Report("PiCoShot", "Salvato: " + saved, ToolTipIcon.Info);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex);
                CloseOverlay();
                Report("PiCoShot", "Salvataggio non riuscito: " + ex.Message, ToolTipIcon.Error);
            }
        }

        /// <summary>Ctrl+P: chiude l'overlay e stampa la selezione.</summary>
        private void PrintSelection()
        {
            if (!HasSelection) return;
            Bitmap crop = Render(_sel);
            CloseOverlay();
            try
            {
                using (var doc = new PrintDocument())
                using (var dlg = new PrintDialog())
                {
                    doc.DocumentName = "PiCoShot";
                    doc.PrintPage += (s, e) =>
                    {
                        RectangleF area = e.MarginBounds;
                        float scale = Math.Min(1f, Math.Min(area.Width / crop.Width, area.Height / crop.Height));
                        float w = crop.Width * scale, h = crop.Height * scale;
                        e.Graphics.DrawImage(crop, area.X, area.Y, w, h);
                        e.HasMorePages = false;
                    };
                    dlg.Document = doc;
                    if (dlg.ShowDialog() == DialogResult.OK) doc.Print();
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex);
                Report("PiCoShot", "Stampa non riuscita: " + ex.Message, ToolTipIcon.Error);
            }
            finally
            {
                crop.Dispose();
            }
        }

        /// <summary>Selezione con le annotazioni (per copia, salvataggio e stampa). L'OCR usa invece Crop().</summary>
        private Bitmap Render(Rectangle r)
        {
            CommitText();
            Bitmap bmp = Crop(r);
            if (_annotations.Count == 0) return bmp;
            using (Graphics g = Graphics.FromImage(bmp))
            {
                Annotation.Prepare(g);
                g.TranslateTransform(-r.X, -r.Y);
                foreach (Annotation a in _annotations) a.Draw(g);
            }
            return bmp;
        }

        private Bitmap Crop(Rectangle r)
        {
            r.Intersect(new Rectangle(0, 0, _shot.Width, _shot.Height));
            var bmp = new Bitmap(r.Width, r.Height, PixelFormat.Format24bppRgb);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.DrawImage(_shot, new Rectangle(0, 0, r.Width, r.Height), r, GraphicsUnit.Pixel);
            }
            return bmp;
        }

        // ---------- tastiera ----------

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (_textBox != null)
            {
                // durante l'inserimento del testo: Esc annulla, Invio conferma, Maiusc+Invio va a capo,
                // le altre combinazioni (Ctrl+C/V/A/Z...) restano alla casella di testo
                if (keyData == Keys.Escape) { DiscardText(); Focus(); return true; }
                if (keyData == Keys.Enter) { CommitText(); return true; }
                return base.ProcessCmdKey(ref msg, keyData);
            }
            switch (keyData)
            {
                case Keys.Control | Keys.Z:
                    Undo();
                    return true;
                case Keys.Escape:
                case Keys.Control | Keys.X:
                    CloseOverlay();
                    return true;
                case Keys.Enter:
                    RunDefaultActions();
                    return true;
                case Keys.Control | Keys.C:
                    CopySelection();
                    return true;
                case Keys.Control | Keys.S:
                    SaveDirect();
                    return true;
                case Keys.Control | Keys.Shift | Keys.S:
                    SaveAs();
                    return true;
                case Keys.Control | Keys.T:
                    CopyText();
                    return true;
                case Keys.Control | Keys.P:
                    PrintSelection();
                    return true;
                case Keys.Control | Keys.A:
                    InvalidateSel();
                    _sel = new Rectangle(Point.Empty, ClientSize);
                    _drag = Drag.None;
                    UpdateToolbar();
                    Invalidate();
                    return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        // ---------- mouse ----------

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left && OnTextFrame(e.Location))
            {
                // afferra la cornice del testo: lo si può spostare finché non lo si conferma
                _drag = Drag.MoveText;
                _textDragFrom = e.Location;
                _textBoxFrom = _textBox.Location;
                return;
            }
            CommitText();
            if (e.Button == MouseButtons.Right)
            {
                if (_sel.IsEmpty) CloseOverlay();
                else
                {
                    InvalidateSel();
                    _sel = Rectangle.Empty;
                    ResetAnnotations();
                    UpdateToolbar();
                    Invalidate();
                }
                return;
            }
            if (e.Button != MouseButtons.Left) return;
            if (_toolbar.Click(e.Location) || _tools.Click(e.Location)) return;

            if (CanDrawAt(e.Location))
            {
                if (_tool == Tool.Text) { BeginText(e.Location); return; }
                _current = CreateAnnotation(e.Location);
                _drag = Drag.Draw;
                return;
            }

            _anchor = e.Location;
            _selAtDragStart = _sel;
            int h = _sel.IsEmpty ? -1 : HitHandle(e.Location);
            if (h >= 0) { _drag = Drag.Resize; _handle = h; }
            else if (!_sel.IsEmpty && _sel.Contains(e.Location)) _drag = Drag.Move;
            else { _drag = Drag.Create; InvalidateSel(); _sel = Rectangle.Empty; ResetAnnotations(); Invalidate(); }
            UpdateToolbar(); // nasconde la toolbar durante il trascinamento
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            Rectangle old = _sel;
            Point oldCursor = _cursor;
            _cursor = e.Location;

            bool overBar = OverBar(e.Location);
            if (_toolbar.MouseMove(e.Location)) Invalidate(_toolbar.TooltipArea);
            if (_tools.MouseMove(e.Location)) Invalidate(_tools.TooltipArea);
            if (_drag == Drag.None && overBar)
            {
                Cursor = Cursors.Default;
                Invalidate(LabelRect(oldCursor));
                return;
            }

            if (_drag == Drag.MoveText)
            {
                Rectangle oldFrame = TextFrame;
                int nx = _textBoxFrom.X + e.X - _textDragFrom.X, ny = _textBoxFrom.Y + e.Y - _textDragFrom.Y;
                nx = Math.Max(TextFrameGap, Math.Min(ClientSize.Width - _textBox.Width - TextFrameGap, nx));
                ny = Math.Max(TextFrameGap, Math.Min(ClientSize.Height - _textBox.Height - TextFrameGap, ny));
                _textBox.Location = new Point(nx, ny);
                Invalidate(Rectangle.Inflate(Rectangle.Union(oldFrame, TextFrame), 2, 2));
                return;
            }
            if (_drag == Drag.Draw)
            {
                Rectangle before = _current.Bounds;
                _current.Update(ClampToSel(e.Location), (ModifierKeys & Keys.Shift) == Keys.Shift);
                Invalidate(Rectangle.Union(before, _current.Bounds));
                return;
            }
            else if (_drag == Drag.Create)
            {
                _sel = Normalize(_anchor, e.Location);
            }
            else if (_drag == Drag.Move)
            {
                int dx = e.X - _anchor.X, dy = e.Y - _anchor.Y;
                Rectangle r = _selAtDragStart;
                r.X = Math.Max(0, Math.Min(ClientSize.Width - r.Width, r.X + dx));
                r.Y = Math.Max(0, Math.Min(ClientSize.Height - r.Height, r.Y + dy));
                _sel = r;
            }
            else if (_drag == Drag.Resize)
            {
                _sel = ResizeFrom(_selAtDragStart, _handle, e.Location);
            }
            else
            {
                UpdateCursor(e.Location);
            }

            // ridisegna solo le aree cambiate (selezione, maniglie, etichetta dimensioni)
            Invalidate(Rectangle.Union(Pad(old), Pad(_sel)));
            Invalidate(LabelRect(oldCursor));
            Invalidate(LabelRect(_cursor));
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            Drag finished = _drag;
            if (finished == Drag.MoveText)
            {
                _drag = Drag.None;
                if (_textBox != null) _textBox.Focus();
                UpdateCursor(e.Location);
                return;
            }
            if (finished == Drag.Draw)
            {
                if (_current != null)
                {
                    _current.Update(ClampToSel(e.Location), (ModifierKeys & Keys.Shift) == Keys.Shift);
                    Invalidate(_current.Bounds);
                    if (_current.IsMeaningful) _annotations.Add(_current);
                    _current = null;
                }
                _drag = Drag.None;
                UpdateCursor(e.Location);
                return;
            }
            if (_drag != Drag.None && (_sel.Width < MinSize || _sel.Height < MinSize))
            {
                InvalidateSel();
                _sel = Rectangle.Empty;
            }
            _drag = Drag.None;

            // Ctrl + trascinamento: copia immediata al rilascio del mouse
            if (finished == Drag.Create && HasSelection && (ModifierKeys & Keys.Control) == Keys.Control)
            {
                CopySelection();
                return;
            }

            UpdateToolbar();
            UpdateCursor(e.Location);
            Invalidate();
        }

        private void UpdateCursor(Point p)
        {
            Cursor c = Cursors.Cross;
            if (OnTextFrame(p)) c = Cursors.SizeAll;
            else if (CanDrawAt(p)) c = _tool == Tool.Text ? Cursors.IBeam : Cursors.Cross;
            else if (!_sel.IsEmpty)
            {
                int h = HitHandle(p);
                if (h >= 0) c = HandleCursor(h);
                else if (_sel.Contains(p)) c = Cursors.SizeAll;
            }
            Cursor = c;
        }

        // ---------- geometria selezione ----------

        private static Rectangle Normalize(Point a, Point b)
        {
            return Rectangle.FromLTRB(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Max(a.X, b.X), Math.Max(a.Y, b.Y));
        }

        private Point[] HandlePoints()
        {
            int l = _sel.Left, t = _sel.Top, r = _sel.Right, b = _sel.Bottom, cx = (l + r) / 2, cy = (t + b) / 2;
            // 0 NW, 1 N, 2 NE, 3 E, 4 SE, 5 S, 6 SW, 7 W
            return new[]
            {
                new Point(l, t), new Point(cx, t), new Point(r, t), new Point(r, cy),
                new Point(r, b), new Point(cx, b), new Point(l, b), new Point(l, cy)
            };
        }

        private int HitHandle(Point p)
        {
            Point[] pts = HandlePoints();
            for (int i = 0; i < pts.Length; i++)
                if (Math.Abs(p.X - pts[i].X) <= HandleSize && Math.Abs(p.Y - pts[i].Y) <= HandleSize) return i;
            return -1;
        }

        private static Cursor HandleCursor(int h)
        {
            switch (h)
            {
                case 0: case 4: return Cursors.SizeNWSE;
                case 2: case 6: return Cursors.SizeNESW;
                case 1: case 5: return Cursors.SizeNS;
                default: return Cursors.SizeWE;
            }
        }

        private Rectangle ResizeFrom(Rectangle s, int h, Point p)
        {
            int l = s.Left, t = s.Top, r = s.Right, b = s.Bottom;
            int x = Math.Max(0, Math.Min(ClientSize.Width, p.X));
            int y = Math.Max(0, Math.Min(ClientSize.Height, p.Y));
            if (h == 0 || h == 6 || h == 7) l = x;
            if (h == 2 || h == 3 || h == 4) r = x;
            if (h == 0 || h == 1 || h == 2) t = y;
            if (h == 4 || h == 5 || h == 6) b = y;
            // consente di "scavalcare" il lato opposto
            return Rectangle.FromLTRB(Math.Min(l, r), Math.Min(t, b), Math.Max(l, r), Math.Max(t, b));
        }

        private static Rectangle Pad(Rectangle r)
        {
            if (r.IsEmpty) return r;
            r.Inflate(HandleSize + 2, HandleSize + 2);
            return r;
        }

        private void InvalidateSel()
        {
            if (!_sel.IsEmpty) Invalidate(Pad(_sel));
        }

        private static Rectangle LabelRect(Point cursor)
        {
            return new Rectangle(cursor.X, cursor.Y, 160, 60);
        }

        // ---------- rendering ----------

        protected override void OnPaintBackground(PaintEventArgs e) { }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (_shot == null) { e.Graphics.Clear(Color.Black); return; }
            Graphics g = e.Graphics;
            g.CompositingMode = CompositingMode.SourceCopy;
            g.DrawImage(_dimmed, e.ClipRectangle, e.ClipRectangle, GraphicsUnit.Pixel);

            if (!_sel.IsEmpty)
            {
                Rectangle vis = Rectangle.Intersect(_sel, e.ClipRectangle);
                if (!vis.IsEmpty) g.DrawImage(_shot, vis, vis, GraphicsUnit.Pixel);
            }

            g.CompositingMode = CompositingMode.SourceOver;
            if (!_sel.IsEmpty && (_annotations.Count > 0 || _current != null))
            {
                GraphicsState state = g.Save();
                g.SetClip(_sel, CombineMode.Intersect);
                Annotation.Prepare(g);
                foreach (Annotation a in _annotations) a.Draw(g);
                if (_current != null) _current.Draw(g);
                g.Restore(state);
            }
            if (_textBox != null)
            {
                Rectangle f = TextFrame;
                using (var dark = new Pen(Color.FromArgb(200, 0, 0, 0), 1))
                using (var light = new Pen(Color.White, 1) { DashStyle = DashStyle.Dash })
                {
                    g.DrawRectangle(dark, f.X, f.Y, f.Width, f.Height);
                    g.DrawRectangle(light, f.X, f.Y, f.Width, f.Height);
                }
            }
            if (!_sel.IsEmpty)
            {
                using (var pen = new Pen(Color.FromArgb(0, 150, 255), 1))
                    g.DrawRectangle(pen, _sel.X, _sel.Y, _sel.Width - 1, _sel.Height - 1);

                if (_drag != Drag.Create)
                {
                    using (var fill = new SolidBrush(Color.White))
                    using (var border = new Pen(Color.FromArgb(0, 150, 255)))
                    {
                        foreach (Point p in HandlePoints())
                        {
                            var hr = new Rectangle(p.X - 3, p.Y - 3, 6, 6);
                            g.FillRectangle(fill, hr);
                            g.DrawRectangle(border, hr);
                        }
                    }
                }
                if (!OverBar(_cursor) && _tool == Tool.None) DrawSizeLabel(g);
            }
            _toolbar.Paint(g);
            _tools.Paint(g);
        }

        private void DrawSizeLabel(Graphics g)
        {
            string text = _sel.Width + " × " + _sel.Height;
            using (var font = new Font("Segoe UI", 9f))
            {
                Size ts = TextRenderer.MeasureText(g, text, font);
                var box = new Rectangle(_cursor.X + 16, _cursor.Y + 20, ts.Width + 6, ts.Height + 2);
                if (box.Right > ClientSize.Width) box.X = _cursor.X - box.Width - 8;
                if (box.Bottom > ClientSize.Height) box.Y = _cursor.Y - box.Height - 8;
                using (var bg = new SolidBrush(Color.FromArgb(200, 30, 30, 30)))
                    g.FillRectangle(bg, box);
                TextRenderer.DrawText(g, text, font, new Point(box.X + 3, box.Y + 1), Color.White);
            }
        }

        // ---------- finestra ----------

        protected override void WndProc(ref Message m)
        {
            // il form deve restare 1:1 con i pixel del desktop virtuale: ignora il ridimensionamento DPI
            if (m.Msg == WM_DPICHANGED) return;
            base.WndProc(ref m);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) DisposeBitmaps();
            base.Dispose(disposing);
        }
    }
}
