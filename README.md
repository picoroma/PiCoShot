# PiCoShot

Clone "Lightshot-like" portable per Windows: cattura area, annotazioni, salvataggio/copia e OCR offline. Specifiche in [SPEC.md](SPEC.md).

**Stato:** Fasi 1-5 implementate (cattura, toolbar/salvataggio/stampa, OCR offline, annotazioni, impostazioni). Upload/condivisione online: non previsto (eventuale Fase 6).

## Schermate

**Selezione dell'area con la toolbar**: lo schermo viene congelato e oscurato, l'area scelta resta in chiaro con maniglie di ridimensionamento e dimensioni in pixel.

![Selezione dell'area con toolbar](docs/screenshots/01-selezione.png)

**Annotazioni al volo**: evidenziatore, riquadro, freccia e testo, inclusi nell'immagine copiata o salvata.

![Annotazioni](docs/screenshots/02-annotazioni.png)

**OCR offline** (`Ctrl+T` o pulsante OCR): il testo dell'area finisce negli appunti e una notifica ne mostra le prime righe.

![Notifica OCR](docs/screenshots/03-ocr.png)

## Uso
- `Stamp` (o `Ctrl+Shift+S` se Stamp è occupato) → overlay di selezione. `Ctrl+Stamp` → schermo intero già selezionato (quello sotto il cursore). Entrambe le hotkey si cambiano da Impostazioni (quella dello schermo intero si può disattivare).
- Trascina per selezionare; maniglie per ridimensionare, trascina dentro per spostare.
- `Invio` / `Ctrl+C` copia negli appunti · `Esc`, `Ctrl+X` o clic destro annulla.
- `Ctrl+S` salva il PNG nella cartella delle Impostazioni · `Ctrl+Shift+S` salva con nome (PNG/JPG/BMP) · `Ctrl+P` stampa.
- `Ctrl+T` copia negli appunti il testo dell'area (OCR offline di Windows, serve la funzionalità OCR della lingua installata).
- `Ctrl+A` seleziona tutto lo schermo · `Ctrl`+trascinamento copia subito al rilascio.
- Toolbar sotto la selezione (si sposta sopra/dentro se manca spazio): linea, freccia, riquadro, cerchio, penna, evidenziatore, testo, colori, spessore e azioni. `Ctrl+Z` annulla l'ultima annotazione; con lo strumento Testo compaiono 4 dimensioni e gli stili grassetto, corsivo ed etichetta su sfondo. `Maiusc` vincola il tratto (l'evidenziatore è dritto, a mano libera con `Maiusc`).
- Impostazioni (menu tray): hotkey (area e schermo intero), cartella screenshot, lingua OCR, colore predefinito delle annotazioni e avvio con Windows. Tutto salvato in `PiCoShot.ini` accanto all'exe (se la cartella non è scrivibile: `%APPDATA%\PiCoShot`).
- "Avvio con Windows" scrive `HKCU\...\Run` solo se attivato dall'utente (menu tray o Impostazioni) e lo rimuove se disattivato.
- Menu tray → Informazioni su PiCoShot.

## PiCoShot.ini
```ini
[General]
Hotkey=PrintScreen            ; selezione area (es. F3, Ctrl+Shift+S)
HotkeyFullScreen=Ctrl+PrintScreen   ; schermo intero, "None" = disattivata
SaveFolder=%USERPROFILE%\Pictures\PiCoShot
OcrLanguage=auto              ; oppure un tag: it, en-US, ...
AutoStart=false
DefaultColor=#FF0000
```

## Build
```
dotnet build src\PiCoShot\PiCoShot.csproj -c Release
```
Output: `src\PiCoShot\bin\Release\net48\PiCoShot.exe`.
