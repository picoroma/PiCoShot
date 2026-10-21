using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Windows.Globalization;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage.Streams;

namespace PiCoShot
{
    /// <summary>Nessun motore OCR disponibile: manca la funzionalità OCR per le lingue installate.</summary>
    internal sealed class OcrUnavailableException : Exception
    {
        public OcrUnavailableException(string message) : base(message) { }
    }

    /// <summary>Wrapper asincrono su Windows.Media.Ocr (motore integrato in Windows, nessuna rete).</summary>
    internal static class OcrService
    {
        private static string MissingLanguageMessage
        {
            get
            {
                return L.T(
                    "Nessuna lingua OCR installata.\n" +
                    "Aggiungi la funzionalità OCR da Impostazioni → Ora e lingua → Lingua e area geografica → " +
                    "(lingua) → Opzioni linguistiche → Riconoscimento del testo (OCR).",
                    "No OCR language installed.\n" +
                    "Add the OCR feature from Settings → Time & language → Language & region → " +
                    "(language) → Language options → Optical character recognition (OCR).");
            }
        }

        private static OcrEngine _engine;
        private static string _engineFor;   // valore di Settings.OcrLanguage con cui è stato creato _engine

        /// <summary>Da chiamare quando cambia la lingua OCR nelle impostazioni.</summary>
        public static void Reset() { _engine = null; _engineFor = null; }

        /// <summary>Lingue con riconoscimento OCR installato in Windows: (tag, nome).</summary>
        public static System.Collections.Generic.List<Tuple<string, string>> AvailableLanguages()
        {
            var list = new System.Collections.Generic.List<Tuple<string, string>>();
            try
            {
                foreach (Language l in OcrEngine.AvailableRecognizerLanguages)
                    list.Add(Tuple.Create(l.LanguageTag, l.DisplayName));
            }
            catch (Exception ex) { Log.Error(ex); }
            return list;
        }

        /// <summary>Lingua scelta nelle impostazioni; se manca: profilo utente, poi italiano, poi inglese.</summary>
        private static OcrEngine GetEngine()
        {
            string wanted = Settings.OcrLanguage ?? "auto";
            if (_engine != null && _engineFor == wanted) return _engine;

            OcrEngine e = null;
            if (!string.Equals(wanted, "auto", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var lang = new Language(wanted);
                    if (OcrEngine.IsLanguageSupported(lang)) e = OcrEngine.TryCreateFromLanguage(lang);
                }
                catch { }
            }
            if (e == null) e = OcrEngine.TryCreateFromUserProfileLanguages();
            foreach (string tag in new[] { "it", "en" })
            {
                if (e != null) break;
                try
                {
                    var lang = new Language(tag);
                    if (OcrEngine.IsLanguageSupported(lang)) e = OcrEngine.TryCreateFromLanguage(lang);
                }
                catch { }
            }
            if (e == null) throw new OcrUnavailableException(MissingLanguageMessage);
            _engine = e;
            _engineFor = wanted;
            return e;
        }

        /// <summary>Riconosce il testo dell'immagine; le righe sono separate da interruzioni di riga.</summary>
        public static async Task<string> RecognizeAsync(Bitmap source)
        {
            OcrEngine engine = GetEngine();

            // altezza < 100 px: upscaling 2x per migliorare la precisione su testo piccolo
            float scale = source.Height < 100 ? 2f : 1f;
            // rispetta il limite dell'API
            float max = OcrEngine.MaxImageDimension;
            float fit = Math.Min(1f, max / Math.Max(source.Width * scale, source.Height * scale));
            scale *= fit;

            Bitmap work = scale == 1f ? source : Resize(source, scale);
            try
            {
                using (var ms = new MemoryStream())
                {
                    work.Save(ms, ImageFormat.Png);
                    using (var ras = new InMemoryRandomAccessStream())
                    {
                        using (var output = ras.GetOutputStreamAt(0))
                        using (var writer = new DataWriter(output))
                        {
                            writer.WriteBytes(ms.ToArray());
                            await writer.StoreAsync();
                            await writer.FlushAsync();
                            writer.DetachStream();
                        }
                        ras.Seek(0);
                        BitmapDecoder decoder = await BitmapDecoder.CreateAsync(ras);
                        using (SoftwareBitmap bmp = await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied))
                        {
                            OcrResult result = await engine.RecognizeAsync(bmp);
                            var sb = new StringBuilder();
                            foreach (OcrLine line in result.Lines)
                            {
                                if (sb.Length > 0) sb.Append("\r\n");
                                sb.Append(line.Text);
                            }
                            return sb.ToString();
                        }
                    }
                }
            }
            finally
            {
                if (!ReferenceEquals(work, source)) work.Dispose();
            }
        }

        private static Bitmap Resize(Bitmap src, float scale)
        {
            int w = Math.Max(1, (int)Math.Round(src.Width * scale));
            int h = Math.Max(1, (int)Math.Round(src.Height * scale));
            var dst = new Bitmap(w, h, PixelFormat.Format24bppRgb);
            using (Graphics g = Graphics.FromImage(dst))
            {
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.DrawImage(src, 0, 0, w, h);
            }
            return dst;
        }
    }
}
