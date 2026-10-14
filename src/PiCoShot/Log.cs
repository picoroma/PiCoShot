using System;
using System.IO;

namespace PiCoShot
{
    /// <summary>Log minimale (solo errori) in PiCoShot.log accanto all'exe, fallback su %APPDATA%.</summary>
    internal static class Log
    {
        public static void Error(Exception ex)
        {
            if (ex == null) return;
            string line = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " " + ex + Environment.NewLine;
            foreach (string dir in new[]
            {
                AppDomain.CurrentDomain.BaseDirectory,
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PiCoShot")
            })
            {
                try
                {
                    Directory.CreateDirectory(dir);
                    File.AppendAllText(Path.Combine(dir, "PiCoShot.log"), line);
                    return;
                }
                catch { }
            }
        }
    }
}
