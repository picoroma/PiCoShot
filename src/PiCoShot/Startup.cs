using System;
using System.Windows.Forms;
using Microsoft.Win32;

namespace PiCoShot
{
    /// <summary>
    /// Avvio con Windows tramite HKCU\...\Run. L'unica scrittura nel registro dell'app,
    /// eseguita solo se l'utente attiva l'opzione (e rimossa se la disattiva).
    /// </summary>
    internal static class Startup
    {
        private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string ValueName = "PiCoShot";

        public static bool IsEnabled
        {
            get
            {
                try
                {
                    using (RegistryKey k = Registry.CurrentUser.OpenSubKey(RunKey, false))
                        return k != null && k.GetValue(ValueName) != null;
                }
                catch { return false; }
            }
        }

        /// <summary>Attiva (con il percorso attuale dell'exe) o disattiva l'avvio automatico; false se il registro non è scrivibile.</summary>
        public static bool Set(bool enabled)
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.CreateSubKey(RunKey))
                {
                    if (k == null) return false;
                    if (enabled) k.SetValue(ValueName, "\"" + Application.ExecutablePath + "\"");
                    else if (k.GetValue(ValueName) != null) k.DeleteValue(ValueName, false);
                }
                return true;
            }
            catch (Exception ex)
            {
                Log.Error(ex);
                return false;
            }
        }
    }
}
