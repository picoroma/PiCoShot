using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Text;

namespace PiCoShot
{
    /// <summary>Scrittura su disco degli output di cattura: immagine PNG e testo OCR accodato.</summary>
    internal static class Output
    {
        /// <summary>Limite di dimensione del file di testo OCR: oltre si passa a un nuovo file numerato.</summary>
        public const long MaxOcrFileBytes = 10L * 1024 * 1024 * 1024;

        public const string OcrFileBase = "PiCoShot_OCR";

        /// <summary>Salva il PNG nella cartella delle Impostazioni (PiCoShot_yyyyMMdd_HHmmss.png) e ne restituisce il percorso.</summary>
        public static string SaveImage(Bitmap image)
        {
            string folder = Settings.ResolvedSaveFolder;
            Directory.CreateDirectory(folder);
            string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string path = Path.Combine(folder, "PiCoShot_" + stamp + ".png");
            for (int n = 2; File.Exists(path); n++)
                path = Path.Combine(folder, "PiCoShot_" + stamp + "_" + n + ".png");
            image.Save(path, ImageFormat.Png);
            return path;
        }

        /// <summary>
        /// Accoda il testo (con data/ora) a PiCoShot_OCR.txt nella cartella screenshot. Se la voce supererebbe i 10 GB
        /// continua in PiCoShot_OCR_2.txt, _3, ... Restituisce il percorso del file usato.
        /// </summary>
        public static string AppendOcr(string text)
        {
            string folder = Settings.ResolvedSaveFolder;
            Directory.CreateDirectory(folder);

            string entry = "=== " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " ===\r\n" + text.TrimEnd() + "\r\n\r\n";
            byte[] data = new UTF8Encoding(false).GetBytes(entry);

            string path = null;
            for (int n = 1; ; n++)
            {
                path = Path.Combine(folder, OcrFileBase + (n == 1 ? "" : "_" + n) + ".txt");
                if (!File.Exists(path) || new FileInfo(path).Length + data.Length <= MaxOcrFileBytes) break;
            }

            bool isNew = !File.Exists(path);
            using (var fs = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read))
            {
                if (isNew)
                {
                    byte[] bom = new UTF8Encoding(true).GetPreamble();   // Blocco note riconosce subito l'UTF-8
                    fs.Write(bom, 0, bom.Length);
                }
                fs.Write(data, 0, data.Length);
            }
            return path;
        }
    }
}
