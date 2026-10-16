using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace PiCoShot
{
    /// <summary>Contesto applicativo: icona in tray, menu, hotkey e overlay pre-creato.</summary>
    internal sealed class TrayApp : ApplicationContext
    {
        private const uint MOD_CONTROL = 0x0002;
        private const uint MOD_SHIFT = 0x0004;

        private const int AreaId = 0;        // hotkey: selezione area
        private const int FullScreenId = 1;  // hotkey: schermo intero

        private readonly NotifyIcon _tray;
        private readonly HotkeyManager _hotkey;
        private readonly CaptureOverlay _overlay;
        private HotkeySpec _currentArea;   // hotkey effettivamente registrate
        private HotkeySpec _currentFull;
        private readonly ToolStripMenuItem _autoItem;

        public TrayApp()
        {
            // overlay pre-istanziato e nascosto: alla cattura si limita a mostrarsi
            _overlay = new CaptureOverlay();
            IntPtr unused = _overlay.Handle;
            _overlay.Notify += (title, text, icon) => _tray.ShowBalloonTip(3000, title, text, icon);

            var menu = new ContextMenuStrip();
            menu.Items.Add("Cattura area", null, (s, e) => Capture(false));
            menu.Items.Add("Cattura schermo intero", null, (s, e) => Capture(true));
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Impostazioni...", null, (s, e) => OpenSettings());
            _autoItem = new ToolStripMenuItem("Avvio con Windows") { CheckOnClick = true };
            _autoItem.Click += (s, e) => SetAutoStart(_autoItem.Checked);
            menu.Items.Add(_autoItem);
            menu.Items.Add("Informazioni su PiCoShot...", null, (s, e) => { using (var f = new AboutForm()) f.ShowDialog(); });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Esci", null, (s, e) => ExitApp());

            _tray = new NotifyIcon
            {
                Icon = BuildIcon(),
                Text = "PiCoShot",
                ContextMenuStrip = menu,
                Visible = true
            };
            _tray.DoubleClick += (s, e) => Capture(false);

            _hotkey = new HotkeyManager();
            _hotkey.Pressed += id => Capture(id == FullScreenId);
            // mentre l'overlay è aperto le hotkey globali vanno rilasciate, altrimenti ruberebbero le scorciatoie
            // dell'overlay (es. Ctrl+Shift+S); alla chiusura vengono registrate di nuovo
            _overlay.VisibleChanged += (s, e) =>
            {
                if (_overlay.Visible) return;
                if (_currentArea.IsValid) _hotkey.Register(AreaId, _currentArea.Modifiers, _currentArea.Key);
                if (_currentFull.IsValid) _hotkey.Register(FullScreenId, _currentFull.Modifiers, _currentFull.Key);
            };
            RegisterHotkeys();

            // avvio con Windows: solo se l'utente lo ha attivato (l'ini lo ricorda; qui si aggiorna il percorso dell'exe)
            if (Settings.AutoStart) Startup.Set(true);
            _autoItem.Checked = Startup.IsEnabled;
        }

        /// <summary>Attiva/disattiva l'avvio con Windows; in caso di errore ripristina lo stato reale.</summary>
        private void SetAutoStart(bool enabled)
        {
            if (Startup.Set(enabled))
            {
                Settings.AutoStart = enabled;
                Settings.Save();
            }
            else
            {
                _tray.ShowBalloonTip(5000, "PiCoShot", "Impossibile modificare l'avvio con Windows (registro non scrivibile).", ToolTipIcon.Warning);
            }
            _autoItem.Checked = Startup.IsEnabled;
        }

        private void RegisterHotkeys()
        {
            // --- selezione area ---
            HotkeySpec hk = Settings.Hotkey;
            if (_hotkey.Register(AreaId, hk.Modifiers, hk.Key)) _currentArea = hk;
            else
            {
                string hint = hk.Key == Keys.PrintScreen
                    ? "\nPer liberare Stamp: Impostazioni → Accessibilità → Tastiera → disattiva \"Usa il tasto Stamp per aprire la cattura schermo\"."
                    : "";
                var fallback = new HotkeySpec { Modifiers = MOD_CONTROL | MOD_SHIFT, Key = Keys.S };
                if (hk.ToString() != fallback.ToString() && _hotkey.Register(AreaId, fallback.Modifiers, fallback.Key))
                {
                    _currentArea = fallback;
                    _tray.ShowBalloonTip(6000, "PiCoShot",
                        hk + " è occupato da un altro programma. Uso " + fallback + " (cambiala da Impostazioni)." + hint,
                        ToolTipIcon.Warning);
                }
                else
                {
                    _tray.ShowBalloonTip(8000, "PiCoShot",
                        "Impossibile registrare " + hk + ". Scegline un'altra da Impostazioni oppure cattura dal menu dell'icona." + hint,
                        ToolTipIcon.Error);
                }
            }

            // --- schermo intero (facoltativa) ---
            HotkeySpec full = Settings.HotkeyFullScreen;
            if (!full.IsValid) return;
            if (full.ToString() == _currentArea.ToString() || !_hotkey.Register(FullScreenId, full.Modifiers, full.Key))
            {
                _tray.ShowBalloonTip(6000, "PiCoShot",
                    "Hotkey schermo intero (" + full + ") non disponibile: è già usata. Cambiala da Impostazioni.",
                    ToolTipIcon.Warning);
                return;
            }
            _currentFull = full;
        }

        private void OpenSettings()
        {
            using (var dlg = new SettingsForm())
            {
                if (dlg.ShowDialog() != DialogResult.OK) return;

                var problems = new System.Text.StringBuilder();
                HotkeySpec area = dlg.Hotkey, full = dlg.HotkeyFullScreen;

                if (area.IsValid && full.IsValid && area.ToString() == full.ToString())
                {
                    problems.AppendLine("Le due hotkey devono essere diverse: schermo intero non modificata.");
                    full = Settings.HotkeyFullScreen;
                }

                // area
                if (area.ToString() != _currentArea.ToString())
                {
                    if (_hotkey.Register(AreaId, area.Modifiers, area.Key))
                    {
                        Settings.Hotkey = area;
                        _currentArea = area;
                    }
                    else
                    {
                        if (_currentArea.IsValid) _hotkey.Register(AreaId, _currentArea.Modifiers, _currentArea.Key);
                        problems.AppendLine(area + " è già usata da un altro programma: la hotkey area resta " + _currentArea + ".");
                    }
                }

                // schermo intero
                if (full.ToString() != _currentFull.ToString())
                {
                    if (!full.IsValid)
                    {
                        _hotkey.Unregister(FullScreenId);
                        _currentFull = full;
                        Settings.HotkeyFullScreen = full;
                    }
                    else if (_hotkey.Register(FullScreenId, full.Modifiers, full.Key))
                    {
                        _currentFull = full;
                        Settings.HotkeyFullScreen = full;
                    }
                    else
                    {
                        if (_currentFull.IsValid) _hotkey.Register(FullScreenId, _currentFull.Modifiers, _currentFull.Key);
                        problems.AppendLine(full + " è già usata da un altro programma: la hotkey schermo intero resta " +
                                            (_currentFull.IsValid ? _currentFull.ToString() : "disattivata") + ".");
                    }
                }

                Settings.SaveFolder = dlg.SaveFolder;
                Settings.DefaultColor = dlg.DefaultColor;
                Settings.DefaultActions = dlg.DefaultActions;
                if (!string.Equals(Settings.OcrLanguage, dlg.OcrLanguage, StringComparison.OrdinalIgnoreCase))
                {
                    Settings.OcrLanguage = dlg.OcrLanguage;
                    OcrService.Reset();
                }
                if (dlg.AutoStart != Startup.IsEnabled || dlg.AutoStart != Settings.AutoStart)
                {
                    if (Startup.Set(dlg.AutoStart)) Settings.AutoStart = dlg.AutoStart;
                    else problems.AppendLine("Impossibile modificare l'avvio con Windows (registro non scrivibile).");
                }
                _autoItem.Checked = Startup.IsEnabled;
                Settings.Save();

                if (problems.Length > 0)
                    MessageBox.Show(problems.ToString().TrimEnd(), "PiCoShot", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        /// <summary>fullScreen: mostra l'overlay con già selezionato l'intero schermo sotto il cursore.</summary>
        private void Capture(bool fullScreen)
        {
            if (_overlay.IsActive) return;
            try
            {
                // prima la cattura, poi qualsiasi finestra
                Rectangle preselect = Rectangle.Empty;
                if (fullScreen)
                {
                    Rectangle v = ScreenCapture.VirtualBounds;
                    preselect = Screen.FromPoint(Cursor.Position).Bounds;
                    preselect.Offset(-v.X, -v.Y);
                }
                Bitmap shot = ScreenCapture.CaptureVirtualDesktop();
                _overlay.ShowCapture(shot, preselect);
                _hotkey.UnregisterAll();
            }
            catch (Exception ex)
            {
                Log.Error(ex);
            }
        }

        private void ExitApp()
        {
            _tray.Visible = false;
            _hotkey.Dispose();
            _overlay.Dispose();
            _tray.Dispose();
            ExitThread();
        }

        public static void NotifyAlreadyRunning()
        {
            using (var ni = new NotifyIcon { Icon = BuildIcon(), Visible = true })
            {
                ni.ShowBalloonTip(3000, "PiCoShot", "PiCoShot è già in esecuzione", ToolTipIcon.Info);
                var sw = Stopwatch.StartNew();
                while (sw.ElapsedMilliseconds < 3000)
                {
                    Application.DoEvents();
                    System.Threading.Thread.Sleep(50);
                }
                ni.Visible = false;
            }
        }

        /// <summary>Icona dell'applicazione, letta dall'exe stesso (incorporata con ApplicationIcon).</summary>
        private static Icon BuildIcon()
        {
            try
            {
                Icon ico = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
                if (ico != null) return ico;
            }
            catch { }
            return SystemIcons.Application;
        }
    }
}
