# PiCoShot — Specifiche di progetto

> Clone "Lightshot-like" per Windows: cattura rapida di un'area dello schermo, annotazioni al volo, salvataggio/copia e **OCR offline** del testo catturato.
> Documento destinato a Claude Code per la realizzazione. Versione 0.1 — 2026-10-14.

---

## 1. Obiettivi

| # | Obiettivo | Misura di successo |
|---|-----------|--------------------|
| G1 | **Velocità** pari a Lightshot | Overlay visibile < 150 ms dalla pressione dell'hotkey |
| G2 | **Portable** | Un solo `PiCoShot.exe` (+ eventuale `PiCoShot.ini` creato a runtime). Nessun installer, nessun runtime aggiuntivo, nessuna scrittura nel registro (salvo opzione "Avvio con Windows") |
| G3 | **Leggero** | Exe < 1 MB, RAM a riposo < 30 MB |
| G4 | **OCR offline** | Testo dell'area selezionata copiato negli appunti, senza connessione di rete |
| G5 | **Privacy** | Nessuna chiamata di rete, mai. Nessun upload, nessuna telemetria |

### Fuori perimetro (v1)
- Upload/condivisione online (è il motivo per cui Lightshot è bloccato in azienda). Eventualmente in una Fase 6 finale, opzionale e disattivata di default.
- Ricerca immagini simili (Google) ed editor online (Pixlr): richiedono rete.
- Funzioni AI / LLM.
- Registrazione video/GIF.
- Ricerca immagini simili.

---

## 2. Stack tecnico (vincolante)

- **Linguaggio:** C#
- **UI:** WinForms
- **Target:** **.NET Framework 4.8** (preinstallato su Windows 10 1903+ e Windows 11) → nessun runtime da distribuire.
- **Progetto:** csproj SDK-style con `<TargetFramework>net48</TargetFramework>`, `<OutputType>WinExe</OutputType>`, `<UseWindowsForms>true</UseWindowsForms>`, riferimento NuGet `Microsoft.NETFramework.ReferenceAssemblies` per compilare con `dotnet build`.
- **OCR:** API WinRT `Windows.Media.Ocr` (motore OCR integrato in Windows), accesso via NuGet `Microsoft.Windows.SDK.Contracts` (10.0.19041.x).
- **Cattura schermo:** GDI (`Graphics.CopyFromScreen` / `BitBlt`) sull'intero desktop virtuale.
- **Hotkey globale:** `RegisterHotKey` (Win32) — vedi §6 per il caso Stamp.
- **Nessuna dipendenza** di terze parti a runtime. Se la build produce DLL aggiuntive, vanno incorporate nell'exe (risorse embedded + `AppDomain.AssemblyResolve`) per mantenere il single-file.

### Requisiti DPI
- Manifest con `PerMonitorV2` DPI awareness (app.manifest), altrimenti la cattura su monitor con scaling ≠ 100% risulta sfocata o sfalsata.

---

## 3. Architettura

```
PiCoShot.exe
├── Program.cs            Entry point, single-instance (Mutex), avvio in tray
├── TrayApp.cs            ApplicationContext: NotifyIcon, menu, registrazione hotkey
├── HotkeyManager.cs      RegisterHotKey / UnregisterHotKey, finestra messaggi nascosta
├── ScreenCapture.cs      Cattura desktop virtuale (tutti i monitor) in Bitmap
├── CaptureOverlay.cs     Form fullscreen senza bordi: mostra lo screenshot congelato,
│                         oscura il resto, gestisce selezione area e maniglie di resize
├── SelectionToolbar.cs   Toolbar agganciata alla selezione (azioni + strumenti)
├── Annotations/          Modello e rendering annotazioni (Arrow, Rect, Pen, Marker, Text)
├── OcrService.cs         Wrapper async su Windows.Media.Ocr
├── Settings.cs           Lettura/scrittura PiCoShot.ini accanto all'exe
└── app.manifest          DPI awareness PerMonitorV2
```

**Principio chiave di velocità:** il processo resta **sempre residente** in tray. Alla pressione dell'hotkey:
1. cattura immediata dello schermo in memoria (prima di mostrare qualsiasi finestra);
2. il form overlay è **già istanziato e nascosto** (pre-creato all'avvio) → si limita a ricevere la bitmap e diventare visibile.

---

## 4. Requisiti funzionali

### 4.1 Tray
- Icona in tray con tooltip "PiCoShot".
- Menu: **Cattura** · **Impostazioni** · **Apri cartella screenshot** · **Avvio con Windows** (check) · **Esci**.
- Doppio clic sull'icona = Cattura.
- Single instance: un secondo avvio mostra notifica "PiCoShot è già in esecuzione" ed esce.

### 4.2 Cattura e selezione
- Hotkey (default **Stamp**) → overlay su tutti i monitor con lo screenshot congelato, oscurato al 40%.
- Trascinamento del mouse = selezione rettangolare; l'area selezionata appare non oscurata.
- Dimensioni della selezione (L × A px) mostrate vicino al cursore.
- Selezione ridimensionabile con 8 maniglie e spostabile trascinandola.
- **Esc** o **Ctrl+X** = annulla e chiudi overlay. **Ctrl+C** = copia. **Ctrl+S** = salva. **Invio** = copia e chiudi.
- **Ctrl+A** = seleziona l'intero desktop virtuale (come "Maximize" di Lightshot).
- **Ctrl+trascinamento**: al rilascio del mouse l'area viene copiata negli appunti e l'overlay si chiude (copia immediata, come Lightshot).
- Clic destro senza selezione = annulla.

### 4.3 Azioni (toolbar)
| Azione | Scorciatoia | Comportamento |
|--------|-------------|---------------|
| Copia immagine | Ctrl+C | Selezione (con annotazioni) negli appunti come immagine, chiude overlay |
| Salva | Ctrl+S | Salva PNG nella cartella delle Impostazioni con nome `PiCoShot_yyyyMMdd_HHmmss.png`, senza finestra di dialogo; **Ctrl+Shift+S** apre "Salva con nome" (PNG, JPG, BMP; PNG predefinito) |
| Stampa | Ctrl+P | Stampa la selezione (con annotazioni) |
| **Copia testo (OCR)** | Ctrl+T | Esegue OCR sulla selezione, copia il testo negli appunti, mostra un toast con le prime righe |
| Chiudi | Esc | Annulla |

### 4.4 OCR
- Motore: `OcrEngine.TryCreateFromUserProfileLanguages()`; fallback a italiano, poi inglese.
- Se nessuna lingua OCR è installata: messaggio chiaro con istruzioni (Impostazioni → Ora e lingua → Lingua → aggiungi funzionalità OCR).
- Preserva le interruzioni di riga del risultato (`OcrLine`).
- Per migliorare la precisione su testo piccolo: se l'altezza della selezione < 100 px, upscaling 2× prima dell'OCR.
- Esecuzione asincrona: l'overlay non deve bloccarsi; mostra cursore di attesa.
- Limite API: dimensione massima immagine `OcrEngine.MaxImageDimension` → ridimensionare se necessario.

### 4.5 Annotazioni (fase 4)
- Strumenti: **Linea** (sottolineatura), **Freccia**, **Riquadro**, **Cerchio/ellisse**, **Penna libera**, **Evidenziatore** (semitrasparente, dritto di default, a mano libera con Maiusc), **Testo**.
- Toolbar unica e compatta a icone (strumenti | colori | spessore | azioni) con suggerimento al passaggio del mouse.
- Maiusc vincola linea/freccia (0°/45°/90°), riquadro/ellisse (quadrato/cerchio) e penna (retta).
- Selettore colore (6 colori predefiniti + personalizzato) e spessore (3 livelli).
- **Ctrl+Z** annulla l'ultima annotazione.
- Le annotazioni sono incluse in copia/salvataggio, **non** nell'OCR (l'OCR lavora sull'immagine originale).

### 4.6 Impostazioni (`PiCoShot.ini`, accanto all'exe)
```ini
[General]
Hotkey=PrintScreen
SaveFolder=%USERPROFILE%\Pictures\PiCoShot
OcrLanguage=auto
AutoStart=false
DefaultColor=#FF0000
```
- Se la cartella dell'exe non è scrivibile, fallback a `%APPDATA%\PiCoShot\PiCoShot.ini`.

---

## 5. Requisiti non funzionali
- Funziona su Windows 10 (1903+) e Windows 11, x64.
- Supporto multi-monitor con scaling DPI diversi per monitor.
- Nessun accesso di rete (verificabile: nessun uso di `System.Net`).
- Nessun privilegio amministrativo richiesto.
- Gestione errori: nessuna eccezione non gestita visibile all'utente; log minimale in `PiCoShot.log` accanto all'ini (solo errori).

---

## 6. Note tecniche e rischi
1. **Tasto Stamp:** su Windows 11 è spesso associato allo Strumento di cattura ("Usa il tasto Stamp per aprire la cattura schermo"). Se `RegisterHotKey(VK_SNAPSHOT)` fallisce o il tasto è intercettato:
   - mostrare notifica con istruzioni per disattivare l'opzione in Impostazioni → Accessibilità → Tastiera;
   - consentire hotkey alternativa da ini (es. `Ctrl+Shift+S`).
2. **DPI:** testare con scaling 100%, 125%, 150% e con monitor misti.
3. **Policy aziendali:** exe non firmato → può essere bloccato da AppLocker/SmartScreen. Fuori dallo scope del codice; valutare in seguito la firma.
4. **OneDrive:** la cartella progetto è sincronizzata da OneDrive. Escludere `bin/` e `obj/` (`.gitignore`) per evitare sincronizzazioni inutili.

---

## 7. Piano di realizzazione (una fase alla volta)

> **Istruzione per Claude Code:** implementa **una fase per volta**. Al termine di ogni fase: compila, verifica i criteri di accettazione, riepiloga cosa è stato fatto e **fermati in attesa di conferma** prima della fase successiva.

### Fase 1 — Scheletro e cattura
- Progetto csproj net48, manifest DPI, single instance, tray con menu (Cattura, Esci).
- Hotkey globale, cattura desktop virtuale, overlay con selezione area.
- Ctrl+C / Invio copia l'area negli appunti; Esc annulla.
- **Accettazione:** exe avviato da doppio clic, nessuna installazione; hotkey → overlay istantaneo; l'area copiata si incolla correttamente in Paint/Teams; funziona su 2 monitor.

### Fase 2 — Toolbar e salvataggio
- Toolbar agganciata alla selezione; maniglie di resize; indicatore dimensioni.
- Salvataggio PNG nella cartella delle Impostazioni e "Salva con nome" (PNG/JPG/BMP); stampa Ctrl+P.
- Ctrl+A (seleziona tutto), Ctrl+X (chiudi), Ctrl+trascinamento (copia immediata).
- **Accettazione:** file PNG salvato con nome corretto; toolbar sempre visibile anche con selezioni a bordo schermo.

### Fase 3 — OCR
- `OcrService` con `Windows.Media.Ocr`, pulsante e scorciatoia Ctrl+T, toast di conferma.
- **Accettazione:** testo italiano e inglese da una pagina web e da una finestra SAP GUI estratto correttamente; nessuna connessione di rete; messaggio chiaro se la lingua OCR manca.

### Fase 4 — Annotazioni
- Strumenti, colori, spessori, Ctrl+Z.
- **Accettazione:** annotazioni presenti nell'immagine copiata/salvata, assenti nel risultato OCR.

### Fase 5 — Impostazioni e rifiniture
- File ini, hotkey configurabile, avvio con Windows (chiave `HKCU\...\Run`, solo se attivato dall'utente), finestra Impostazioni minimale, icona applicazione.
- **Accettazione:** copiando l'exe su un altro PC e avviandolo, funziona senza altri passaggi.

---

## 8. Build e distribuzione
- Build: `dotnet build -c Release` (oppure `msbuild`), output in `bin\Release\net48\`.
- Deliverable: `PiCoShot.exe` (single file). Verificare che non servano DLL accanto all'exe; se presenti, incorporarle (§2).
- Versioning: `AssemblyVersion` semantico, partendo da `0.1.0`.

## 9. Struttura cartella progetto
```
PiCoShot\
├── SPEC.md
├── README.md
├── .gitignore
├── PiCoShot.sln
└── src\PiCoShot\
    ├── PiCoShot.csproj
    ├── app.manifest
    ├── Resources\picoshot.ico
    └── *.cs
```
