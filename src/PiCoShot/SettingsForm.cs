using System;
using System.Drawing;
using System.Windows.Forms;

namespace PiCoShot
{
    /// <summary>Pannello Impostazioni: hotkey, cartella screenshot, lingua OCR, colore predefinito, avvio con Windows.</summary>
    internal sealed class SettingsForm : Form
    {
        private sealed class LangItem
        {
            public string Tag, Text;
            public override string ToString() { return Text; }
        }

        private readonly TextBox _areaBox = new TextBox();
        private readonly TextBox _fullBox = new TextBox();
        private readonly TextBox _folderBox = new TextBox();
        private readonly ComboBox _ocrBox = new ComboBox();
        private readonly Button _colorButton = new Button();
        private readonly CheckBox _autoStart = new CheckBox();
        private readonly CheckBox _actCopy = new CheckBox();
        private readonly CheckBox _actSave = new CheckBox();
        private readonly CheckBox _actOcr = new CheckBox();
        private HotkeySpec _area, _full;
        private Color _color;

        public HotkeySpec Hotkey { get { return _area; } }
        public HotkeySpec HotkeyFullScreen { get { return _full; } }
        public string SaveFolder { get { return _folderBox.Text.Trim(); } }
        public string OcrLanguage { get { var i = _ocrBox.SelectedItem as LangItem; return i == null ? "auto" : i.Tag; } }
        public Color DefaultColor { get { return _color; } }
        public bool AutoStart { get { return _autoStart.Checked; } }
        public DefaultAction DefaultActions
        {
            get
            {
                DefaultAction a = DefaultAction.None;
                if (_actCopy.Checked) a |= DefaultAction.Copy;
                if (_actSave.Checked) a |= DefaultAction.Save;
                if (_actOcr.Checked) a |= DefaultAction.Ocr;
                return a;
            }
        }

        public SettingsForm()
        {
            _area = Settings.Hotkey;
            _full = Settings.HotkeyFullScreen;
            _color = Settings.DefaultColor;

            Text = "PiCoShot - Impostazioni";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterScreen;
            MaximizeBox = MinimizeBox = false;
            ShowInTaskbar = false;
            Font = SystemFonts.MessageBoxFont;
            ClientSize = new Size(440, 410);

            // --- hotkey ---
            var hotkeysGroup = new GroupBox { Text = "Hotkey (clicca nel campo e premi la combinazione)", Left = 12, Top = 8, Width = 416, Height = 108 };
            var areaLabel = new Label { Text = "Selezione area:", Left = 12, Top = 28, Width = 110 };
            _areaBox.SetBounds(126, 25, 278, 24);
            _areaBox.ReadOnly = true;
            _areaBox.Text = _area.ToString();
            _areaBox.KeyDown += (s, e) => OnHotkeyKeyDown(e, false);

            var fullLabel = new Label { Text = "Schermo intero:", Left = 12, Top = 66, Width = 110 };
            _fullBox.SetBounds(126, 63, 208, 24);
            _fullBox.ReadOnly = true;
            _fullBox.Text = FullText();
            _fullBox.KeyDown += (s, e) => OnHotkeyKeyDown(e, true);
            var none = new Button { Text = "Nessuna", Left = 340, Top = 62, Width = 64, Height = 26 };
            none.Click += (s, e) => { _full = new HotkeySpec(); _fullBox.Text = FullText(); };
            hotkeysGroup.Controls.AddRange(new Control[] { areaLabel, _areaBox, fullLabel, _fullBox, none });

            // --- cartella ---
            var folderLabel = new Label { Text = "Cartella di salvataggio screenshot:", Left = 12, Top = 128, Width = 416 };
            _folderBox.SetBounds(12, 150, 336, 24);
            _folderBox.Text = Settings.SaveFolder;
            var browse = new Button { Text = "Sfoglia...", Left = 354, Top = 149, Width = 74, Height = 26 };
            browse.Click += OnBrowse;

            // --- lingua OCR e colore ---
            var ocrLabel = new Label { Text = "Lingua OCR:", Left = 12, Top = 188, Width = 250 };
            _ocrBox.SetBounds(12, 210, 250, 24);
            _ocrBox.DropDownStyle = ComboBoxStyle.DropDownList;
            FillLanguages();

            var colorLabel = new Label { Text = "Colore annotazioni:", Left = 276, Top = 188, Width = 152 };
            _colorButton.SetBounds(276, 209, 152, 26);
            _colorButton.Text = "Scegli...";
            _colorButton.TextAlign = ContentAlignment.MiddleRight;
            _colorButton.Paint += (s, e) =>
            {
                using (var br = new SolidBrush(_color))
                    e.Graphics.FillRectangle(br, 6, 5, 28, _colorButton.Height - 11);
                e.Graphics.DrawRectangle(Pens.Gray, 6, 5, 28, _colorButton.Height - 11);
            };
            _colorButton.Click += (s, e) =>
            {
                using (var dlg = new ColorDialog { Color = _color, FullOpen = true })
                    if (dlg.ShowDialog(this) == DialogResult.OK) { _color = dlg.Color; _colorButton.Invalidate(); }
            };

            // --- azione predefinita (pulsante OK ✔ e Invio) ---
            var actGroup = new GroupBox { Text = "Azione predefinita del pulsante ✔ / Invio (anche più di una)", Left = 12, Top = 246, Width = 416, Height = 94 };
            _actCopy.Text = "Copia immagine"; _actCopy.SetBounds(12, 22, 120, 22);
            _actSave.Text = "Salva immagine"; _actSave.SetBounds(140, 22, 120, 22);
            _actOcr.Text = "OCR su file"; _actOcr.SetBounds(270, 22, 130, 22);
            _actCopy.Checked = (Settings.DefaultActions & DefaultAction.Copy) != 0;
            _actSave.Checked = (Settings.DefaultActions & DefaultAction.Save) != 0;
            _actOcr.Checked = (Settings.DefaultActions & DefaultAction.Ocr) != 0;
            var actHint = new Label
            {
                Text = "OCR su file: il testo viene accodato a PiCoShot_OCR.txt nella cartella screenshot e copiato negli appunti.",
                Left = 12, Top = 46, Width = 392, Height = 40, ForeColor = SystemColors.GrayText
            };
            actGroup.Controls.AddRange(new Control[] { _actCopy, _actSave, _actOcr, actHint });

            // --- avvio ---
            _autoStart.Text = "Avvia PiCoShot con Windows";
            _autoStart.SetBounds(12, 348, 416, 24);
            _autoStart.Checked = Startup.IsEnabled;   // stato reale (registro o StartupTask)

            var ok = new Button { Text = "OK", Left = 272, Top = 372, Width = 75, DialogResult = DialogResult.OK };
            var cancel = new Button { Text = "Annulla", Left = 353, Top = 372, Width = 75, DialogResult = DialogResult.Cancel };
            AcceptButton = ok;
            CancelButton = cancel;

            Controls.AddRange(new Control[]
            {
                hotkeysGroup, folderLabel, _folderBox, browse, ocrLabel, _ocrBox, colorLabel, _colorButton, actGroup, _autoStart, ok, cancel
            });
            ActiveControl = _folderBox;
        }

        private void FillLanguages()
        {
            _ocrBox.Items.Add(new LangItem { Tag = "auto", Text = "Automatica (lingue di Windows)" });
            bool found = string.Equals(Settings.OcrLanguage, "auto", StringComparison.OrdinalIgnoreCase);
            foreach (var l in OcrService.AvailableLanguages())
            {
                _ocrBox.Items.Add(new LangItem { Tag = l.Item1, Text = l.Item2 + " (" + l.Item1 + ")" });
                if (string.Equals(l.Item1, Settings.OcrLanguage, StringComparison.OrdinalIgnoreCase)) found = true;
            }
            if (!found) _ocrBox.Items.Add(new LangItem { Tag = Settings.OcrLanguage, Text = Settings.OcrLanguage + " (non installata)" });

            foreach (LangItem i in _ocrBox.Items)
                if (string.Equals(i.Tag, Settings.OcrLanguage, StringComparison.OrdinalIgnoreCase)) _ocrBox.SelectedItem = i;
            if (_ocrBox.SelectedIndex < 0) _ocrBox.SelectedIndex = 0;
        }

        private string FullText()
        {
            return _full.IsValid ? _full.ToString() : "(nessuna)";
        }

        private void OnBrowse(object sender, EventArgs e)
        {
            using (var dlg = new FolderBrowserDialog())
            {
                dlg.Description = "Scegli la cartella dove salvare gli screenshot";
                dlg.ShowNewFolderButton = true;
                string current = Environment.ExpandEnvironmentVariables(_folderBox.Text.Trim());
                if (System.IO.Directory.Exists(current)) dlg.SelectedPath = current;
                if (dlg.ShowDialog(this) == DialogResult.OK) _folderBox.Text = dlg.SelectedPath;
            }
        }

        private void OnHotkeyKeyDown(KeyEventArgs e, bool fullScreen)
        {
            Keys k = e.KeyCode;
            e.Handled = e.SuppressKeyPress = true;
            if (k == Keys.ControlKey || k == Keys.ShiftKey || k == Keys.Menu || k == Keys.LWin || k == Keys.RWin)
                return;

            uint mods = 0;
            if (e.Control) mods |= HotkeySpec.MOD_CONTROL;
            if (e.Alt) mods |= HotkeySpec.MOD_ALT;
            if (e.Shift) mods |= HotkeySpec.MOD_SHIFT;

            bool standalone = k == Keys.PrintScreen || (k >= Keys.F1 && k <= Keys.F24);
            if (mods == 0 && !standalone) return; // un tasto normale senza modificatori bloccherebbe la digitazione

            var spec = new HotkeySpec { Modifiers = mods, Key = k };
            if (fullScreen) { _full = spec; _fullBox.Text = FullText(); }
            else { _area = spec; _areaBox.Text = _area.ToString(); }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (DialogResult == DialogResult.OK)
            {
                if (DefaultActions == DefaultAction.None)
                {
                    MessageBox.Show(this, "Seleziona almeno un'azione predefinita.", "PiCoShot", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    e.Cancel = true;
                    return;
                }
                if (SaveFolder.Length == 0)
                {
                    MessageBox.Show(this, "Indica una cartella di salvataggio.", "PiCoShot", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    e.Cancel = true;
                    return;
                }
                try
                {
                    System.IO.Directory.CreateDirectory(Environment.ExpandEnvironmentVariables(SaveFolder));
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, "Cartella non utilizzabile:\n" + ex.Message, "PiCoShot", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    e.Cancel = true;
                    return;
                }
            }
            base.OnFormClosing(e);
        }
    }
}
