# Scheda Microsoft Store: bozza

*Bozza dei testi per Partner Center. Evita nomi di marchi di terzi (altre app di screenshot, titoli di film). Sostituisci i segnaposto [..].*

## Checklist di invio

- [x] Account sviluppatore: <https://storedeveloper.microsoft.com> → "Get started" → Individual developer (gratuito) → ID + selfie
- [x] Nome "PiCoShot" riservato in Partner Center (Apps and games → New product → MSIX or PWA app)
- [ ] `Identity Name`, `Publisher` e `Publisher display name` copiati da Product management → Product identity
- [ ] Pacchetto: `.\build.ps1` poi `.\packaging\msix\build-msix.ps1 -IdentityName ".." -Publisher "CN=.." -PublisherDisplayName ".."`
- [x] Privacy policy pubblicata: https://picoroma.github.io/PiCoShot/privacy/
- [ ] Schermate nuove (almeno 1, consigliate 4; 16:9, 1366×768 o più)
- [ ] Questionario classificazione per età
- [ ] Giustificazione `runFullTrust` (testo sotto)
- [ ] Prova su PC pulito + Windows App Certification Kit sul pacchetto

## Proprietà

- **Nome:** PiCoShot
- **Categoria:** Utilità e strumenti (Utilities & tools)
- **Prezzo:** Gratuita
- **Lingue della scheda:** italiano (+ inglese consigliato)
- **Sito / supporto:** https://picoroma.github.io/PiCoShot/ · picodev@cocco.biz
- **URL informativa privacy:** https://picoroma.github.io/PiCoShot/privacy/

## Italiano

### Descrizione breve (una riga)
Cattura un'area dello schermo, annotala al volo e copia il testo con l'OCR, tutto offline.

### Descrizione
PiCoShot è un piccolo strumento per catturare lo schermo in modo rapido.

Premi la scorciatoia (Stamp di default), trascina per scegliere l'area e fai quello che ti serve: copiarla, salvarla, annotarla o estrarne il testo.

**Cosa puoi fare**
• Cattura un'area o l'intero schermo, anche con più monitor.
• Annota con linee, frecce, riquadri, cerchi, penna, evidenziatore e testo (dimensioni e stili a scelta).
• OCR offline: copia il testo di un'area negli appunti, oppure accodalo a un file di testo. Funziona con il motore di riconoscimento integrato in Windows.
• Azione predefinita con un clic (✔): copia, salva su file, OCR su file, anche insieme.
• Salva in PNG, JPG o BMP, copia negli appunti, stampa.
• Scorciatoie personalizzabili, cartella di salvataggio e lingua OCR a scelta.

**Privacy**
Nessuna connessione a internet, nessun account, nessuna pubblicità, nessuna telemetria. Tutto resta sul tuo dispositivo.

### Novità di questa versione (0.2.0)
• Pulsante ✔ per eseguire in un clic l'azione predefinita (copia, salva, OCR su file) e ✖ per annullare.
• Strumenti di annotazione in colonna a lato della selezione.
• OCR accodato a un file di testo.

### Parole chiave (max 7)
screenshot, cattura schermo, ocr, annota, appunti, schermata, testo da immagine

### Note per la certificazione (inglese)
PiCoShot is a Win32 (.NET Framework 4.8, WinForms) desktop utility packaged as MSIX with the `runFullTrust` capability. Full trust is required because the app: (1) registers global hotkeys (RegisterHotKey) to start a capture from any application, (2) captures the screen with GDI (Graphics.CopyFromScreen), and (3) runs in the notification area (system tray). It makes no network requests, collects no data and has no accounts. Start-at-sign-in is opt-in, via the packaged StartupTask (disabled by default).
How to test: launch the app (a tray icon appears); press PrintScreen (or Ctrl+Shift+S if PrintScreen is taken by another app); drag to select an area; use the toolbar (annotate, copy, save, OCR); click the green check to run the default action, or the red X / Esc to cancel. OCR needs the Windows OCR feature for the selected language (installed by default for the system language).

## English

### Short description
Capture part of your screen, annotate it on the fly and copy its text with OCR, all offline.

### Description
PiCoShot is a small tool for quick screen captures.

Press the shortcut (Print Screen by default), drag to choose the area and do what you need: copy it, save it, annotate it or extract its text.

**What you can do**
• Capture an area or the whole screen, including multi-monitor setups.
• Annotate with lines, arrows, rectangles, circles, pen, highlighter and text (sizes and styles of your choice).
• Offline OCR: copy the text of an area to the clipboard, or append it to a text file. Uses the text recognition engine built into Windows.
• One-click default action (✔): copy, save to file, OCR to file, in any combination.
• Save as PNG, JPG or BMP, copy to clipboard, print.
• Customizable shortcuts, save folder and OCR language.

**Privacy**
No internet connection, no account, no ads, no telemetry. Everything stays on your device.

### What's new (0.2.0)
• ✔ button runs your default action (copy, save, OCR to file) in one click; ✖ cancels.
• Annotation tools in a column beside the selection.
• OCR text appended to a text file.

### Search terms (max 7)
screenshot, screen capture, ocr, annotate, clipboard, snip, text from image

## Classificazione per età (indicazioni per il questionario IARC)
Utility senza contenuti generati dagli utenti condivisi, senza violenza, senza gioco d'azzardo, senza acquisti, senza condivisione di posizione, senza chat. Risposte attese: tutte "No". Esito atteso: tutte le età.

## Schermate da preparare (layout 0.2.0)
1. Selezione dell'area con colonna strumenti a destra e barra sotto (✔/✖ visibili).
2. Annotazioni: riquadro, freccia, testo, evidenziatore.
3. OCR: notifica con il testo riconosciuto.
4. Impostazioni: hotkey, azione predefinita, lingua OCR.
*Usa dati di esempio neutri (niente nomi di server, clienti o dati reali).*
