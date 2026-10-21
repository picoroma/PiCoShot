using System;
using System.Collections.Generic;
using System.IO;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace PiCoShot
{
    /// <summary>Azioni eseguite dal pulsante OK (✔) / Invio; combinabili.</summary>
    [Flags]
    internal enum DefaultAction { None = 0, Copy = 1, Save = 2, Ocr = 4 }

    /// <summary>Hotkey = modificatori + tasto, serializzabile come "Ctrl+Shift+S" / "PrintScreen".</summary>
    internal struct HotkeySpec
    {
        public const uint MOD_ALT = 0x0001, MOD_CONTROL = 0x0002, MOD_SHIFT = 0x0004, MOD_WIN = 0x0008;

        public uint Modifiers;
        public Keys Key;

        public static readonly HotkeySpec Default = new HotkeySpec { Modifiers = 0, Key = Keys.PrintScreen };

        public bool IsValid { get { return Key != Keys.None; } }

        public override string ToString()
        {
            var sb = new StringBuilder();
            if ((Modifiers & MOD_CONTROL) != 0) sb.Append("Ctrl+");
            if ((Modifiers & MOD_ALT) != 0) sb.Append("Alt+");
            if ((Modifiers & MOD_SHIFT) != 0) sb.Append("Shift+");
            if ((Modifiers & MOD_WIN) != 0) sb.Append("Win+");
            sb.Append(Key == Keys.PrintScreen ? "PrintScreen" : Key.ToString());
            return sb.ToString();
        }

        public static bool TryParse(string text, out HotkeySpec spec)
        {
            spec = new HotkeySpec();
            if (string.IsNullOrWhiteSpace(text)) return false;
            foreach (string raw in text.Split('+'))
            {
                string p = raw.Trim();
                if (p.Length == 0) return false;
                switch (p.ToLowerInvariant())
                {
                    case "ctrl": case "control": spec.Modifiers |= MOD_CONTROL; continue;
                    case "alt": spec.Modifiers |= MOD_ALT; continue;
                    case "shift": spec.Modifiers |= MOD_SHIFT; continue;
                    case "win": case "windows": spec.Modifiers |= MOD_WIN; continue;
                    case "printscreen": case "stamp": case "prtsc": case "snapshot":
                        spec.Key = Keys.PrintScreen; continue;
                }
                Keys k;
                if (Enum.TryParse(p, true, out k) && k != Keys.None && (k & Keys.Modifiers) == 0
                    && k != Keys.ControlKey && k != Keys.ShiftKey && k != Keys.Menu)
                    spec.Key = k;
                else
                    return false;
            }
            return spec.IsValid;
        }
    }

    /// <summary>Lettura/scrittura di PiCoShot.ini accanto all'exe (fallback %APPDATA%\PiCoShot).</summary>
    internal static class Settings
    {
        private const string FileName = "PiCoShot.ini";
        public const string DefaultSaveFolder = @"%USERPROFILE%\Pictures\PiCoShot";

        public static HotkeySpec Hotkey = HotkeySpec.Default;   // selezione area
        // schermo intero: predefinita Ctrl+Stamp; Key == None = disattivata
        public static HotkeySpec HotkeyFullScreen = new HotkeySpec { Modifiers = HotkeySpec.MOD_CONTROL, Key = Keys.PrintScreen };
        public static string SaveFolder = DefaultSaveFolder;   // può contenere variabili d'ambiente
        public static DefaultAction DefaultActions = DefaultAction.Copy;   // predefinita: copia (come prima di 0.2.0)
        public static bool AutoStart;                          // avvio con Windows
        public static string Language = "auto";                // lingua dell'interfaccia: "auto" (come Windows), "it" o "en"
        public static string OcrLanguage = "auto";             // "auto" = lingue del profilo utente, oppure un tag (it, en-US, ...)
        public static Color DefaultColor = Color.FromArgb(255, 0, 0);   // colore iniziale delle annotazioni

        public static string IniPath { get; private set; }

        /// <summary>Cartella di salvataggio con le variabili d'ambiente espanse.</summary>
        public static string ResolvedSaveFolder
        {
            get { return Environment.ExpandEnvironmentVariables(SaveFolder); }
        }

        public static void Load()
        {
            IniPath = ResolvePath();
            try
            {
                if (!File.Exists(IniPath)) { Save(); return; }
                HotkeySpec spec;
                if (HotkeySpec.TryParse(Read("Hotkey"), out spec)) Hotkey = spec;
                string full = Read("HotkeyFullScreen");
                if (full != null)
                {
                    if (HotkeySpec.TryParse(full, out spec)) HotkeyFullScreen = spec;
                    else HotkeyFullScreen = new HotkeySpec();   // vuota o "None": disattivata
                }
                string acts = Read("DefaultActions");
                if (acts != null)
                {
                    DefaultAction parsed = DefaultAction.None;
                    foreach (string part in acts.Split(new[] { ',', ';', '+', ' ' }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        DefaultAction one;
                        if (Enum.TryParse(part, true, out one) && one != DefaultAction.None) parsed |= one;
                    }
                    if (parsed != DefaultAction.None) DefaultActions = parsed;   // vuota/non valida: resta la copia
                }
                bool b;
                if (bool.TryParse(Read("AutoStart"), out b)) AutoStart = b;
                string lang = Read("OcrLanguage");
                if (!string.IsNullOrWhiteSpace(lang)) OcrLanguage = lang;
                string col = Read("DefaultColor");
                if (!string.IsNullOrWhiteSpace(col))
                {
                    try { DefaultColor = ColorTranslator.FromHtml(col); } catch { }
                }
                string ui = Read("Language");
                if (!string.IsNullOrWhiteSpace(ui)) Language = ui.Trim();
                string folder = Read("SaveFolder");
                if (!string.IsNullOrWhiteSpace(folder)) SaveFolder = folder;
            }
            catch (Exception ex) { Log.Error(ex); }
        }

        public static void Save()
        {
            try
            {
                var lines = File.Exists(IniPath) ? new List<string>(File.ReadAllLines(IniPath)) : new List<string>();
                if (!lines.Exists(l => l.Trim().Equals("[General]", StringComparison.OrdinalIgnoreCase)))
                    lines.Insert(0, "[General]");
                Write(lines, "Hotkey", Hotkey.ToString());
                Write(lines, "HotkeyFullScreen", HotkeyFullScreen.IsValid ? HotkeyFullScreen.ToString() : "None");
                Write(lines, "SaveFolder", SaveFolder);
                Write(lines, "DefaultActions", DefaultActions.ToString().Replace(" ", ""));
                Write(lines, "Language", Language);
                Write(lines, "OcrLanguage", OcrLanguage);
                Write(lines, "AutoStart", AutoStart ? "true" : "false");
                Write(lines, "DefaultColor", "#" + (DefaultColor.ToArgb() & 0xFFFFFF).ToString("X6"));
                File.WriteAllLines(IniPath, lines.ToArray(), new UTF8Encoding(false));
            }
            catch (Exception ex) { Log.Error(ex); }
        }

        private static void Write(List<string> lines, string key, string value)
        {
            for (int i = 0; i < lines.Count; i++)
            {
                int eq = lines[i].IndexOf('=');
                if (eq > 0 && lines[i].Substring(0, eq).Trim().Equals(key, StringComparison.OrdinalIgnoreCase))
                {
                    lines[i] = key + "=" + value;
                    return;
                }
            }
            int g = lines.FindIndex(l => l.Trim().Equals("[General]", StringComparison.OrdinalIgnoreCase));
            lines.Insert(g + 1, key + "=" + value);
        }

        private static string Read(string key)
        {
            foreach (string line in File.ReadAllLines(IniPath))
            {
                int eq = line.IndexOf('=');
                if (eq > 0 && line.Substring(0, eq).Trim().Equals(key, StringComparison.OrdinalIgnoreCase))
                    return line.Substring(eq + 1).Trim();
            }
            return null;
        }

        private static string ResolvePath()
        {
            string local = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, FileName);
            string roaming = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PiCoShot", FileName);
            if (File.Exists(local)) return local;
            if (File.Exists(roaming)) return roaming;
            try
            {
                using (new FileStream(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ".write_test"), FileMode.Create, FileAccess.Write, FileShare.None, 1, FileOptions.DeleteOnClose)) { }
                return local;
            }
            catch
            {
                Directory.CreateDirectory(Path.GetDirectoryName(roaming));
                return roaming;
            }
        }
    }
}
