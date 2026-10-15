using System;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;

namespace PiCoShot
{
    /// <summary>Finestra "Informazioni su PiCoShot" con l'immagine incorporata nell'exe.</summary>
    internal sealed class AboutForm : Form
    {
        private const string ImageResource = "PiCoShot.about.jpg";

        public AboutForm()
        {
            Text = "Informazioni su PiCoShot";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterScreen;
            MaximizeBox = MinimizeBox = false;
            ShowInTaskbar = false;
            Font = SystemFonts.MessageBoxFont;
            ClientSize = new Size(380, 612);

            var picture = new PictureBox
            {
                Left = 10, Top = 10, Width = 360, Height = 343,
                SizeMode = PictureBoxSizeMode.Zoom,
                Image = LoadImage()
            };

            // citazione sotto la foto: corsivo, centrata
            var quote = new Label
            {
                Text = "Oh, gioveni! Quando vi dico sequitemi miei pugnaci, dovete sequire et pugnare! " +
                       "Poche conte! Se no qui stemo a prenderci per le natiche.",
                Font = new Font(Font.FontFamily, 10f, FontStyle.Italic),
                Left = 10, Top = 358, Width = 360, Height = 64,
                TextAlign = ContentAlignment.MiddleCenter
            };

            Version v = Assembly.GetExecutingAssembly().GetName().Version;
            var title = new Label
            {
                Text = "PiCoShot " + v.Major + "." + v.Minor + "." + v.Build,
                Font = new Font(Font.FontFamily, 16f, FontStyle.Bold),
                Left = 10, Top = 428, Width = 360, Height = 32,
                TextAlign = ContentAlignment.MiddleCenter
            };
            var text = new Label
            {
                Text = "Cattura rapida di un'area dello schermo, annotazioni al volo,\n" +
                       "salvataggio, stampa e OCR offline del testo catturato.\n\n" +
                       "Portable: un solo file, nessuna installazione.\n" +
                       "Nessuna connessione di rete, nessuna telemetria.",
                Left = 10, Top = 466, Width = 360, Height = 100,
                TextAlign = ContentAlignment.TopCenter
            };
            var ok = new Button { Text = "Chiudi", Left = 150, Top = 572, Width = 80, DialogResult = DialogResult.OK };
            AcceptButton = ok;
            CancelButton = ok;

            Controls.AddRange(new Control[] { picture, quote, title, text, ok });
        }

        private static Image LoadImage()
        {
            try
            {
                using (var s = Assembly.GetExecutingAssembly().GetManifestResourceStream(ImageResource))
                {
                    if (s == null) return null;
                    using (var img = Image.FromStream(s))
                        return new Bitmap(img); // copia: il flusso può essere rilasciato
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex);
                return null;
            }
        }
    }
}
