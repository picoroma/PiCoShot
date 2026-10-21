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
            Text = L.T("Informazioni su PiCoShot", "About PiCoShot");
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
                Text = "O prodi! Qui si cattura lo schermo come falchi affamati! " +
                       "Quando vi dico 'screenshot' voi percutete et salvate! " +
                       "Poche chiacchiere! Se no stemo a grattarci le terga!",
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
                Text = L.T("Cattura rapida di un'area dello schermo, annotazioni al volo,\n", "Quick capture of a screen area, on-the-fly annotations,\n") +
                       L.T("salvataggio, stampa e OCR offline del testo catturato.\n\n", "saving, printing and offline OCR of the captured text.\n\n") +
                       L.T("Portable: un solo file, nessuna installazione.\n", "Portable: a single file, no installation.\n") +
                       L.T("Nessuna connessione di rete, nessuna telemetria.", "No network connection, no telemetry."),
                Left = 10, Top = 466, Width = 360, Height = 100,
                TextAlign = ContentAlignment.TopCenter
            };
            var ok = new Button { Text = L.T("Chiudi", "Close"), Left = 150, Top = 572, Width = 80, DialogResult = DialogResult.OK };
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
