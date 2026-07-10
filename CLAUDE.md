# G13 Flight Panel

MSFS-2020-Flugdaten auf einem Logitech-G13-LCD. .NET 10, Windows-only (WinForms-Tray-App,
kein Konsolenfenster per Default). Vollständige Nutzerdoku: [README.md](README.md).

## Architektur (wichtig für neue Arbeit hier)

Domain/Application/Infrastructure-Ordner mit passenden Namespaces, Repository-Pattern für
LCD-Seiten, DI via `Microsoft.Extensions.DependencyInjection`. `Program.cs` ist die
Composition Root. Details, inkl. Schritt-für-Schritt-Anleitung zum Ergänzen eines neuen
SimVars oder einer neuen Seite: siehe README.md Abschnitt "Erweitern".

`Domain/` kennt nichts Externes. `Application/` orchestriert (`FlightDataVariables`,
`PageRepository`, `Pages/*`), reines Formatieren, kein I/O. `Infrastructure/` redet mit
der Außenwelt (SimConnect, G13-Hardware via P/Invoke gegen `LogitechLcd.dll`, Konsole).

## Bauen/Testen in diesem Environment

- `dotnet build` / `dotnet run` / `dotnet run --demo` (erzwingt Demo-Modus).
- **Kein SimConnect-Live-Test hier möglich** – nur Demo-Modus ist in dieser Umgebung
  testbar. Änderungen am SimConnect-Datenpfad (`Infrastructure/SimConnectFlightDataSource.cs`)
  brauchen eine Bestätigung durch den Nutzer mit echtem laufendem MSFS.
- **G13-Hardware/echte Tasten hier nicht testbar** – Button-Logik ggf. isoliert simulieren
  (siehe Git-Historie für Beispiele) statt anzunehmen, dass es funktioniert.
- Nach jedem Test: `Get-Process -Name G13FlightPanel | Stop-Process -Force` – die App hat
  einen Single-Instance-Mutex-Schutz, ein liegen gebliebener Prozess blockiert den
  nächsten Build/Testlauf (Datei-Lock auf die .exe) bzw. sorgt dafür, dass eine neue
  Instanz sofort mit "läuft bereits" abbricht.

## Bekannte offene Punkte

- Die LOC\*/LOC/G-S\*/G-S-Zahlencodes in `Application/Pages/AutopilotPage.cs`
  (FBW-A32NX-FMA) sind **nicht verifiziert** (nur aus Erinnerung), siehe README
  "Bekannte Stolpersteine". Bei Gelegenheit mit echtem ILS-Anflug prüfen.
- `dev`-Branch ist der aktuelle Arbeitsstand, `master` ist der letzte Alpha-Release-Stand
  (v0.1.0-alpha). Bei größeren Merges ggf. nachfragen, ob/wann `dev` nach `master` soll.

## Arbeitsstil, der sich in diesem Projekt bewährt hat

- Vor größeren Refactors: Plan-Modus nutzen, Design mit einem Plan-Agent gegenprüfen
  lassen, dann explizit vom Nutzer freigeben lassen (mehrere Iterationen sind normal -
  der Nutzer hat hier klare eigene Vorstellungen zur Architektur, z. B. Interfaces statt
  einfacher Delegates, Repository+DI-Pattern explizit gewünscht).
- Nach jeder nicht-trivialen Änderung: tatsächlich bauen und laufen lassen (nicht nur
  Compile-Check), auch wenn nur Demo-Modus testbar ist - in diesem Projekt gab es mehrfach
  Bugs, die erst beim echten Ausführen auffielen (z. B. Off-by-One im Button-Cycling, ein
  Konsolen-Mirror-Bug, der nur nach dem Wechsel auf Tray-Betrieb auftrat).
- Deutsch für Kommentare/Doku/Kommunikation mit dem Nutzer.
