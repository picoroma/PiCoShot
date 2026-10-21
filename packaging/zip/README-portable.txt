PiCoShot - versione portable / portable version
=================================================

ITALIANO
--------
1. Estrai la cartella dove vuoi (anche su una chiavetta USB). Non serve installare nulla.
2. Avvia PiCoShot.exe: compare un'icona nell'area di notifica (vicino all'orologio).
3. Premi Stamp (Print Screen) per catturare un'area; Ctrl+Stamp per lo schermo intero.
   Se Stamp e' occupato da Windows (Strumento di cattura), PiCoShot usa Ctrl+Maiusc+S e te lo dice.
4. Trascina per selezionare. Il pulsante verde esegue l'azione predefinita (copia / salva / OCR su file), la X annulla.
   Clic destro sull'icona nell'area di notifica: Impostazioni, Informazioni, Esci.

Impostazioni e file
- Le impostazioni stanno in PiCoShot.ini accanto a PiCoShot.exe (se la cartella non e' scrivibile: %APPDATA%\PiCoShot).
- Per rimuovere PiCoShot: chiudilo (Esci dall'icona) e cancella la cartella. Se avevi attivato "Avvio con Windows",
  disattivalo prima dalle Impostazioni (la voce e' nel registro, solo del tuo utente).
- Nessuna connessione di rete, nessuna telemetria. L'OCR usa il motore integrato in Windows.

Se Windows mostra "PC protetto da Windows" (SmartScreen)
- Clic su "Ulteriori informazioni" -> "Esegui comunque". L'exe non e' firmato.
- Se il file arriva da internet: tasto destro su PiCoShot.exe -> Proprieta' -> spunta "Sblocca" -> OK.

ENGLISH
-------
1. Extract the folder anywhere (a USB stick works too). Nothing to install.
2. Run PiCoShot.exe: an icon appears in the notification area (near the clock).
3. Press Print Screen to capture an area; Ctrl+Print Screen for the whole screen.
   If Print Screen is taken by Windows (Snipping Tool), PiCoShot uses Ctrl+Shift+S and tells you.
4. Drag to select. The green button runs your default action (copy / save / OCR to file), the X cancels.
   Right-click the tray icon: Settings, About, Exit.

Settings and files
- Settings live in PiCoShot.ini next to PiCoShot.exe (if the folder is read-only: %APPDATA%\PiCoShot).
- To remove PiCoShot: quit it (Exit from the tray icon) and delete the folder. If you enabled "Start with Windows",
  turn it off in Settings first (the entry is in the registry, for your user only).
- No network connection, no telemetry. OCR uses the engine built into Windows.

If Windows shows "Windows protected your PC" (SmartScreen)
- Click "More info" -> "Run anyway". The exe is not code-signed.
- If the file was downloaded: right-click PiCoShot.exe -> Properties -> tick "Unblock" -> OK.

Requirements / Requisiti: Windows 10 (1903+) or 11, 64-bit. .NET Framework 4.8 is built into Windows.

Also available on the Microsoft Store (automatic updates):
https://apps.microsoft.com/detail/9NTK0XXD2BK0

Source code, change log and license (GNU GPL v3): https://github.com/picoroma/PiCoShot
The icon and the images in the About window are excluded from the license.
