using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace PiCoShot
{
    /// <summary>Hotkey globali tramite RegisterHotKey, con finestra messaggi nascosta. Più hotkey, ognuna con un id.</summary>
    internal sealed class HotkeyManager : NativeWindow, IDisposable
    {
        private const int WM_HOTKEY = 0x0312;
        private const int MOD_NOREPEAT = 0x4000;
        private const int IdBase = 0x5043;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        private readonly HashSet<int> _registered = new HashSet<int>();

        /// <summary>Evento con l'id (0, 1, ...) passato a Register.</summary>
        public event Action<int> Pressed;

        public HotkeyManager()
        {
            CreateHandle(new CreateParams { Parent = new IntPtr(-3) }); // HWND_MESSAGE
        }

        /// <summary>Registra l'hotkey <paramref name="id"/>; false se già occupata da un altro programma.</summary>
        public bool Register(int id, uint modifiers, Keys key)
        {
            Unregister(id);
            if (key == Keys.None) return false;
            if (!RegisterHotKey(Handle, IdBase + id, modifiers | MOD_NOREPEAT, (uint)key)) return false;
            _registered.Add(id);
            return true;
        }

        public void Unregister(int id)
        {
            if (_registered.Remove(id)) UnregisterHotKey(Handle, IdBase + id);
        }

        public void UnregisterAll()
        {
            foreach (int id in new List<int>(_registered)) Unregister(id);
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_HOTKEY)
            {
                int id = m.WParam.ToInt32() - IdBase;
                if (_registered.Contains(id))
                {
                    var h = Pressed;
                    if (h != null) h(id);
                    return;
                }
            }
            base.WndProc(ref m);
        }

        public void Dispose()
        {
            UnregisterAll();
            if (Handle != IntPtr.Zero) DestroyHandle();
        }
    }
}
