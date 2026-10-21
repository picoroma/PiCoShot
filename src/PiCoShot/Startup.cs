using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using Microsoft.Win32;
using Windows.ApplicationModel;

namespace PiCoShot
{
    /// <summary>
    /// Avvio con Windows, solo se l'utente lo attiva.
    /// - App normale (exe portable): voce in HKCU\...\Run, rimossa se si disattiva.
    /// - App installata dal pacchetto MSIX (Microsoft Store): il registro è virtualizzato e Windows non lo userebbe,
    ///   quindi si usa l'estensione windows.startupTask del manifest tramite l'API StartupTask.
    /// </summary>
    internal static class Startup
    {
        private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string ValueName = "PiCoShot";
        private const string TaskId = "PiCoShotStartup";   // deve coincidere con il manifest MSIX
        private const int APPMODEL_ERROR_NO_PACKAGE = 15700;

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetCurrentPackageFullName(ref int length, StringBuilder name);

        private static bool? _packaged;

        /// <summary>Motivo dell'ultimo errore di Set (testo da mostrare all'utente), oppure null.</summary>
        public static string LastError { get; private set; }

        /// <summary>true se l'app gira dentro un pacchetto MSIX (identità di pacchetto presente).</summary>
        public static bool IsPackaged
        {
            get
            {
                if (_packaged == null)
                {
                    try
                    {
                        int len = 0;
                        _packaged = GetCurrentPackageFullName(ref len, null) != APPMODEL_ERROR_NO_PACKAGE;
                    }
                    catch { _packaged = false; }   // API non presente: sistema senza pacchetti
                }
                return _packaged.Value;
            }
        }

        public static bool IsEnabled
        {
            get { return IsPackaged ? PackagedIsEnabled() : RegistryIsEnabled(); }
        }

        /// <summary>Attiva (con il percorso attuale dell'exe) o disattiva l'avvio automatico; false se non riuscito (vedi LastError).</summary>
        public static bool Set(bool enabled)
        {
            LastError = null;
            return IsPackaged ? PackagedSet(enabled) : RegistrySet(enabled);
        }

        // ---------- exe portable: registro ----------

        private static bool RegistryIsEnabled()
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(RunKey, false))
                    return k != null && k.GetValue(ValueName) != null;
            }
            catch { return false; }
        }

        private static bool RegistrySet(bool enabled)
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.CreateSubKey(RunKey))
                {
                    if (k == null) { LastError = "Registro non scrivibile."; return false; }
                    if (enabled) k.SetValue(ValueName, "\"" + Application.ExecutablePath + "\"");
                    else if (k.GetValue(ValueName) != null) k.DeleteValue(ValueName, false);
                }
                return true;
            }
            catch (Exception ex)
            {
                Log.Error(ex);
                LastError = "Impossibile modificare l'avvio con Windows (registro non scrivibile).";
                return false;
            }
        }

        // ---------- pacchetto MSIX: StartupTask ----------

        private static StartupTask GetTask()
        {
            return StartupTask.GetAsync(TaskId).AsTask().GetAwaiter().GetResult();
        }

        private static bool PackagedIsEnabled()
        {
            try
            {
                StartupTaskState s = GetTask().State;
                return s == StartupTaskState.Enabled || s == StartupTaskState.EnabledByPolicy;
            }
            catch (Exception ex)
            {
                Log.Error(ex);
                return false;
            }
        }

        private static bool PackagedSet(bool enabled)
        {
            try
            {
                StartupTask task = GetTask();
                if (!enabled)
                {
                    task.Disable();
                    return true;
                }

                StartupTaskState state = task.RequestEnableAsync().AsTask().GetAwaiter().GetResult();
                if (state == StartupTaskState.Enabled || state == StartupTaskState.EnabledByPolicy) return true;

                LastError = state == StartupTaskState.DisabledByUser
                    ? "L'avvio è stato disattivato da te in Windows: riattivalo da Impostazioni > App > Avvio."
                    : state == StartupTaskState.DisabledByPolicy
                        ? "L'avvio automatico è bloccato dai criteri del sistema."
                        : "Impossibile attivare l'avvio con Windows.";
                return false;
            }
            catch (Exception ex)
            {
                Log.Error(ex);
                LastError = "Impossibile modificare l'avvio con Windows.";
                return false;
            }
        }
    }
}
