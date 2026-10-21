# Change log

## In sviluppo (prossima versione)

### Novità
- **Interfaccia in italiano e inglese.** Di default segue la lingua di Windows (italiano se è italiano, altrimenti inglese). Si cambia da Impostazioni → *Lingua dell'app* (o `Language=auto|it|en` nell'ini) e vale dal riavvio successivo. Il motto nell'About resta in italiano.
- Il pacchetto MSIX dichiara anche `en-US`, così lo Store può mostrare la scheda in inglese.

## 0.2.0

### Novità
- **Azione predefinita (pulsante ✔ / Invio).** Dopo aver selezionato l'area, il pulsante verde **✔ OK** (in basso a destra) esegue le azioni predefinite scelte nelle Impostazioni, da una a tutte e tre insieme:
  - **Copia immagine** negli appunti (con le annotazioni);
  - **Salva immagine** su file PNG nella cartella screenshot;
  - **OCR su file**: il testo riconosciuto viene accodato a `PiCoShot_OCR.txt` (UTF-8, ogni voce con data/ora; oltre i 10 GB si passa a `PiCoShot_OCR_2.txt`, ...) e copiato negli appunti.
  Con Copia e OCR attive gli appunti contengono immagine **e** testo insieme (si incolla l'immagine in Paint o Teams, il testo in Blocco note). Una sola notifica riepiloga cosa è stato fatto.
- **Pulsante ✖ Annulla** accanto al ✔: chiude l'overlay senza eseguire nulla (come Esc).
- **Azioni predefinite evidenziate**: Copia, Salva e OCR attivi hanno bordo e pallino verdi nella barra; il suggerimento del ✔ elenca cosa farà ("OK (Invio): copia + salva + OCR su file").
- **Opzioni su due lati**: gli strumenti di annotazione (linea, freccia, riquadro, cerchio, penna, evidenziatore, testo) sono ora in una **colonna a destra** della selezione, mentre colori, spessori/opzioni del testo, azioni e ✔/✖ restano nella **barra sotto**. Se a destra manca spazio la colonna passa a sinistra o dentro la selezione, senza coprire la barra sotto.
- **Impostazioni**: nuovo gruppo "Azione predefinita" con le tre caselle (almeno una obbligatoria). In `PiCoShot.ini`: `DefaultActions=Copy,Save,Ocr` (predefinita: `Copy`, come nella 0.1.0).

### Modifiche
- `Invio` ora esegue l'azione predefinita (prima copiava soltanto); `Ctrl+C`, `Ctrl+S`, `Ctrl+T` restano le azioni singole.
- La versione 0.2.0 compare nell'About e nelle proprietà del file.

## 0.1.0

Prima versione.
- Cattura di un'area o dello schermo intero (hotkey configurabili, default `Stamp` / `Ctrl+Stamp`), anche su più monitor con DPI diversi.
- Annotazioni: linea, freccia, riquadro, cerchio, penna, evidenziatore, testo (4 dimensioni, grassetto, corsivo, etichetta; il testo si sposta prima di confermarlo), colori e spessori, `Ctrl+Z`.
- OCR offline con il motore integrato di Windows (`Ctrl+T`).
- Copia, salva (PNG), salva con nome (PNG/JPG/BMP), stampa.
- Impostazioni in `PiCoShot.ini`: hotkey, cartella screenshot, lingua OCR, colore predefinito, avvio con Windows (opzionale).
- Single file portable, nessuna rete, nessuna telemetria.
