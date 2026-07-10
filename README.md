# G13 Flight Panel

Zeigt Flugdaten aus MSFS 2020 (FBW A32NX) auf dem G13-LCD (4 Textzeilen, 160x43 Pixel)
an. Läuft als Tray-Icon (kein Konsolenfenster) mit Rechtsklick-Menü. Zwei Seiten, per
Taste unter dem Display **oder** per Tray-Menü umschaltbar:

- **Seite 1 (Flugdaten)**: IAS/Heading, Höhe/Vertical Speed, Flap-Lever/Fuel, NAV1-Frequenz/OBS
- **Seite 2 (Autopilot)**: AP-Status + aktiver Modus, Selected Altitude/Heading/Speed

```
IAS 145KT    HDG 270          AP ON        MODE ALT/HDG
ALT 35000FT  VS +1200         ALT SEL 36000FT
FLP 2/4      FUEL 075         HDG SEL 280
NAV 118.10   OBS 270          SPD SEL 260KT
```

## Voraussetzungen

- **Windows 10/11** – nutzt Windows-spezifische APIs (P/Invoke gegen eine native DLL,
  `net10.0-windows`-Target), läuft nicht unter Linux/macOS.
- **.NET 10 SDK** – [dotnet.microsoft.com/download](https://dotnet.microsoft.com/download)
  zum Bauen und Ausführen.
- Optional, für echte Hardware-Ausgabe: ein **Logitech G13** Gameboard + die
  **Logitech Gaming Software (LGS)** – siehe Abschnitt 2. Ohne beides läuft die App trotzdem
  (Tray-Icon, aber ohne LCD-Ausgabe).
- Optional, für echte Flugdaten: **Microsoft Flight Simulator 2020** mit installiertem
  **MSFS SDK** – siehe Abschnitt 3. Ohne SDK/laufendes MSFS läuft die App im Demo-Modus mit
  synthetischen Werten.
- Für die volle Anzeige (Flap-Lever, Autopilot-Modi inkl. LOC/G-S): das
  **FlyByWire A32NX** Mod, da einige Werte FBW-spezifische LVars nutzen (siehe
  "Verwendete SimVars/LVars" unten). Mit anderen Flugzeugen laufen IAS/Höhe/VS/Heading/
  NAV/Fuel/generische AP-Werte trotzdem normal, nur Flap-Lever und LOC\*/G-S\* bleiben leer.

## Installation

1. Repository klonen:
   ```
   git clone https://github.com/cuban8IO/G13FlightPanel.git
   cd G13FlightPanel
   ```
2. Bauen: `dotnet build`
3. Starten: `dotnet run` (oder `dotnet run --demo` für erzwungenen Demo-Modus)

Damit läuft der Demo-Modus (synthetische Werte, Tray-Icon im Infobereich unten rechts) –
ohne G13 oder MSFS nötig. Für die Hardware-Ausgabe bzw. echte Flugdaten die Abschnitte 2
und 3 unten durchgehen.

## Architektur

Grob an Clean Architecture angelehnt (Domain/Application/Infrastructure als Ordner mit
passenden Namespaces), mit Repository-Pattern für die LCD-Seiten und Dependency
Injection als Verdrahtung – bewusst nicht als separate Projekte/Assemblies, dafür ist
dieses Tool zu klein, nur als Ordnerstruktur innerhalb des einen Projekts.

```
Domain/           - reine Datenmodelle/Verträge, keine externen Abhängigkeiten
  FlightData.cs         Datenmodell für einen Snapshot (manche Properties berechnet,
                         z. B. FuelPercent aus FuelQuantityGal/FuelCapacityGal)
  IFlightDataSource.cs   liefert FlightData-Updates per Event
  ILcdPage.cs            eine LCD-Seite: Name + BuildLines(FlightData)
  IPageRepository.cs     Zugriff auf alle registrierten Seiten

Application/      - Logik, die Domain-Objekte orchestriert, aber nichts Externes anspricht
  FlightDataVariables.cs SimVar-Liste, single source of truth (siehe unten)
  PageRepository.cs      IPageRepository-Implementierung (sammelt alle ILcdPage per DI)
  Pages/
    FlightDataPage.cs     Seite 1 (IAS/Höhe/VS/Heading/Flaps/Fuel/NAV)
    AutopilotPage.cs      Seite 2 (AP-Status/Modus/Selected-Werte)

Infrastructure/   - alles, was mit der Außenwelt redet
  DemoFlightDataSource.cs      synthetische Werte, immer verfügbar, kein SDK nötig
  SimConnectFlightDataSource.cs  echte Sim-Daten via SimConnect (nur mit HAVE_SIMCONNECT)
  LcdDisplay.cs                 5x7-Pixel-Font-Rendering, G13-Tasten, Konsolen-Spiegel

Program.cs        - Composition Root: baut den DI-Container, verdrahtet alles
```

`SimConnectFlightDataSource` fällt nach 3 gescheiterten Verbindungsversuchen (~15s)
automatisch auf `DemoFlightDataSource` zurück, statt endlos weiter zu versuchen, und
danach im Hintergrund alle 15s weiter zu MSFS zu verbinden – kein manueller Neustart
nötig, egal in welcher Reihenfolge App und MSFS gestartet werden.

Das Projekt kompiliert und läuft **so wie es ist** (Demo-Modus) – auch ohne LGS und ohne
MSFS SDK. Erst wenn du die beiden SDKs unten einbindest, schaltet es automatisch auf echte
Hardware/Sim-Daten um.

### Tray-Icon

Die App läuft ohne Konsolenfenster im Infobereich der Taskleiste (künstlicher-Horizont-
Icon). Rechtsklick öffnet ein Menü:

- **Konsole anzeigen/ausblenden** – öffnet bei Bedarf ein Konsolenfenster für die
  Diagnose-Ausgaben (Verbindungsstatus, Fehler). Standardmäßig aus, da `Console.WriteLine`
  ohne Konsole einfach ins Leere schreibt statt einen Fehler zu werfen. Zeigt nur neue
  Meldungen ab dem Öffnen, keine Historie.
- **Seite wählen** – Untermenü mit einem Eintrag pro registrierter Seite, direkt per
  Maus wählbar, unabhängig von der physischen G13-Taste.
- **Beenden**

## 1. Jetzt testen (Demo-Modus)

```
dotnet run
dotnet run --demo   # erzwingt Demo-Modus sofort, auch wenn SimConnect eingerichtet ist
```

Es erscheint kein Fenster – nur ein neues Icon im Infobereich der Taskleiste (ggf. über
den Pfeil "Ausgeblendete Symbole einblenden" sichtbar). Rechtsklick → "Konsole anzeigen"
zeigt 4 sich ändernde Datenzeilen. Das prüft nur die Programmlogik – noch ohne G13 oder
Sim. `--demo` ist auch nützlich, um die AP-Seite (inkl. LOC*/G-S-Zyklus) ohne laufendes
MSFS durchzutesten.

## 2. Auf dem Gaming-PC: G13-LCD anschließen

1. Logitech Gaming Software (LGS) installieren – **nicht G HUB**, das unterstützt das
   G13 nicht mehr. LGS ist offiziell EOL, läuft aber auf den meisten Windows-10/11-
   Systemen noch.
2. G13 anschließen, LGS einmal starten.
3. `dotnet build` – kopiert `libs\LogitechLcd.dll` automatisch flach ins Build-Output
   (siehe `.csproj`). `dotnet run`, dann im Tray-Menü "Konsole anzeigen" – die Zeile
   `LcdDisplay` sollte jetzt `G13-LCD gefunden` melden statt des Fallback-Texts.
4. Falls stattdessen `Kein G13/LogitechLcd.dll gefunden`: meist steckt eine
   `DllNotFoundException`/`BadImageFormatException` dahinter, die intern abgefangen wird
   (siehe "Bekannte Stolpersteine" unten für beide Fälle).

### Warum kein `LogiLcdMonoSetText` und kein normaler Font?

`LcdDisplay` rendert Text **selbst** auf ein 160x43-Pixel-Bitmap und schickt es über
`LogiLcdMonoSetBackground` ans Display, statt die einfachere SDK-Funktion
`LogiLcdMonoSetText` (4 Textzeilen, fester SDK-Font) zu nutzen. Zwei einfachere Ansätze
wurden ausprobiert und verworfen:

1. **`LogiLcdMonoSetText`**: der eingebaute Font der Logitech-SDK ist **nicht** monospace
   (Buchstaben sind unterschiedlich breit) – egal wie die Textzeilen mit Leerzeichen/Nullen
   aufgefüllt wurden, die zweite Spalte (z. B. `HDG`/`VS`/`OBS`) ist je nach Zahlengröße
   unterschiedlich weit gewandert.
2. **Selbst gerendert mit GDI+ (`System.Drawing`, TrueType-Font wie Consolas)**: bei nur
   ~10 Pixel Zeilenhöhe ist ein normaler Vektor-Font zu klein, um sauber gerastert zu
   werden – die Buchstaben zerfallen beim Hinting in einzelne Pixel-Fragmente ("sieht nur
   nach Punkten aus").

Die Lösung: ein handgezeichneter 5x7-Pixel-Font (`Font`-Dictionary in
`Infrastructure/LcdDisplay.cs`),
jedes Zeichen ein festes Bitmuster ohne jede Kantenglättung – garantiert scharf und exakt
gleich breit, kein GDI+/`UseWindowsForms` nötig.

### Seitenwechsel (LCD-Tasten)

Jede der 4 Tasten unter dem G13-Display (`LogiLcdIsButtonPressed`, Bitflags `0x1`/`0x2`/
`0x4`/`0x8`) lässt sich frei einer oder mehreren Seiten zuordnen – das Mapping steht in
`LcdDisplay`s Konstruktor (`_buttonPages`). Aktuell: Taste 0 → Flugdaten, Taste 1 →
Autopilot, Tasten 2/3 frei. Teilen sich mehrere Seiten eine Taste, blättert wiederholtes
Drücken zwischen ihnen um (jede Taste merkt sich ihre eigene Position unabhängig von den
anderen). Mit steigender-Flanke-Erkennung, damit ein gehaltener Tastendruck nicht
mehrfach pro Sekunde umschaltet. Alle Seiten sind zusätzlich per Tray-Menü → "Seite
wählen" direkt per Maus wählbar.

## 3. SimConnect anbinden (echte Flugdaten)

1. MSFS SDK installieren (im Sim: `Optionen → Allgemein → Entwickler-Modus` aktivieren,
   dann `Entwicklung → Options` bzw. direkt der SDK-Installer von Microsoft/Asobo).
2. Darin liegt die **managed** Assembly, z. B.
   `C:\MSFS SDK\SimConnect SDK\lib\managed\Microsoft.FlightSimulator.SimConnect.dll`.
   In diesen Projektordner unter `libs\` kopieren (Ordner ggf. anlegen).
3. Zusätzlich die **native** Runtime-DLL (anderer Name, gleiche Familie) aus
   `C:\MSFS SDK\SimConnect SDK\lib\SimConnect.dll` ebenfalls nach `libs\SimConnect.dll`
   kopieren – die `.csproj` kopiert sie dann automatisch mit ins Build-Output.
4. `dotnet build` neu ausführen: Sobald `libs\Microsoft.FlightSimulator.SimConnect.dll`
   existiert, setzt die `.csproj` automatisch `HAVE_SIMCONNECT`, und
   `Infrastructure/SimConnectFlightDataSource.cs` wird mitkompiliert.
5. MSFS mit dem FBW A320 starten, dann `dotnet run` – im Tray-Menü "Konsole anzeigen"
   sollte `Mit MSFS verbunden.` zeigen.

Läuft MSFS nicht oder ist SimConnect nicht erreichbar, versucht die App 3x im 5s-Abstand
zu verbinden und wechselt danach automatisch in den Demo-Modus (kein manuelles Neustarten
nötig, sobald MSFS doch noch hochfährt – dafür einfach die App neu starten).

Falls beim Kompilieren einzelne Member-Namen (z. B. `SIMCONNECT_DATA_REQUEST_FLAG`)
nicht gefunden werden: Die exakte API kann sich zwischen SDK-Versionen leicht
unterscheiden – IntelliSense/Compiler-Fehler zeigen dir direkt den korrekten Namen aus
deiner installierten Version, siehe auch die
[offizielle SimConnect-Referenz](https://docs.flightsimulator.com/html/Programming_Tools/SimConnect/SimConnect_API_Reference.htm).

## Verwendete SimVars/LVars

| Zweck | Variable | Einheit |
|---|---|---|
| IAS | `AIRSPEED INDICATED` | Knots |
| Höhe | `INDICATED ALTITUDE` | Feet |
| Vertical Speed | `VERTICAL SPEED` | Feet per minute |
| Heading | `PLANE HEADING DEGREES MAGNETIC` | Degrees |
| Flap-Lever (FBW) | `L:A32NX_FLAPS_HANDLE_INDEX` | Number |
| NAV1-Frequenz | `NAV ACTIVE FREQUENCY:1` | MHz |
| NAV1-OBS | `NAV OBS:1` | Degrees |
| Fuel (gesamt) | `FUEL TOTAL QUANTITY` / `FUEL TOTAL CAPACITY` | Gallons (daraus % berechnet) |
| AP-Status | `AUTOPILOT MASTER` | Bool |
| ALT/HDG/SPD selected | `AUTOPILOT ALTITUDE LOCK VAR` / `AUTOPILOT HEADING LOCK DIR` / `AUTOPILOT AIRSPEED HOLD VAR` | Feet / Degrees / Knots |
| ALT/VS/HDG/NAV/APPR aktiv (generisch) | `AUTOPILOT ALTITUDE LOCK` / `AUTOPILOT VERTICAL HOLD` / `AUTOPILOT HEADING LOCK` / `AUTOPILOT NAV1 LOCK` / `AUTOPILOT APPROACH HOLD` | Bool |
| LOC\*/LOC, G/S\*/G/S (FBW-FMA) | `L:A32NX_FMA_LATERAL_MODE` / `L:A32NX_FMA_VERTICAL_MODE` | Number (Enum-Code) |

**SEL vs. MODE**: `ALT/HDG/SPD SEL` auf Seite 2 zeigt immer den im FCU eingestellten
**Zielwert** – unabhängig davon, ob der Autopilot diesen Modus gerade aktiv verfolgt (genau
wie am echten FCU: das Zahlenfenster ändert sich nicht, nur eine Lampe/Anzeige daneben
zeigt den aktiven Modus). `MODE` zeigt dagegen den **aktiv verfolgten** Vertikal-/Lateral-
Modus (z. B. `ALT/HDG`, `VS/---`, `APR/NAV` oder `G/S*/LOC*` während eines ILS-Anflugs).

## Bekannte Stolpersteine

- **FBW-LVars generell**: `L:A32NX_FLAPS_HANDLE_INDEX` und die `A32NX_FMA_*`-Variablen
  werden direkt per SimConnect gelesen (geht seit Sim Update 10 / 2022 ohne
  Zusatzsoftware). Namen sind case-sensitive und können sich zwischen FBW-Versionen
  ändern – im Dev-Modus des Sims (`,`-Taste → Behavior Debug) den aktuellen
  Variablennamen/-wert prüfen. Falls das Lesen fehlschlägt: MobiFlight-WASM-Modul
  installieren, das bietet eine robustere LVar-Bridge über Client Data Areas.
- **LOC\*/LOC/G-S\*/G-S-Zahlencodes unsicher**: Die in
  `Application/Pages/AutopilotPage.cs` verwendeten Codes (30=LOC\*, 31=LOC, 90=G/S\*,
  91=G/S für `A32NX_FMA_LATERAL_MODE`/`A32NX_FMA_VERTICAL_MODE`) stammen aus der
  Erinnerung an FBWs internes Enum, **nicht** aus einer verifizierten Quelle - können
  sich zwischen FBW-Versionen unterscheiden. Falls die Anzeige während eines echten
  ILS-Anflugs falsch/blank bleibt: im Dev-Modus den tatsächlichen Wert der beiden LVars
  ablesen und dort anpassen. Fällt bei falschem/fehlendem Code automatisch auf die
  generischen `AUTOPILOT *`-Booleans zurück (ALT/VS/HDG/NAV/APR), nie auf blank/Absturz.
- **LGS-Instabilität**: LGS ist seit Jahren unsupportet. Falls es unter aktuellem
  Windows Probleme macht (Treibersignatur, Abstürze), ist die Alternative ein Wechsel
  auf rohes USB-HID (G13 als generisches HID-Gerät ansprechen, ohne Logitech-Software) –
  deutlich mehr Aufwand, aber unabhängig von LGS.
- **Bitness**: `LogitechLcd.dll` und die native `SimConnect.dll` müssen zur Prozess-
  Architektur passen (`PlatformTarget` in der `.csproj`). Zeigt die Konsole
  `DllNotFoundException: ... Das angegebene Modul wurde nicht gefunden` obwohl die Datei
  im Ordner liegt, ist das meist eine Bitness- oder VC++-Redistributable-Sache, keine
  fehlende Datei.
- **Konsolen-Live-Update**: `LcdDisplay.Render` ruft bei jedem Update `Console.Clear()`
  auf, bevor die 4 Zeilen neu geschrieben werden – ältere Konsolenausgaben (Start-Banner,
  SimConnect-Fehler) gehen dadurch verloren. Ein Versuch, stattdessen nur den 4-zeiligen
  Bereich per `Console.SetCursorPosition` in-place zu überschreiben (absolute und relative
  Zeilenposition, mit/ohne Zeilenumbruch), zeigte in der Praxis wiederholt kaputte/
  eingefrorene Ausgabe aus unklarem Grund und wurde wieder verworfen. Für das eigentliche
  G13-Display spielt das keine Rolle (`RenderToBitmap` ist davon unabhängig).

## Erweitern

### Neuen SimVar/LVar anzeigen

1. In `Domain/FlightData.cs` eine neue Property ergänzen.
2. In `Application/FlightDataVariables.cs` einen neuen Eintrag in `All` ergänzen
   (SimVar-/LVar-Name, Einheit, Zuweisungs-Lambda) – **das ist die einzige Stelle**, die
   sowohl die SimConnect-Registrierung als auch die Extraktion steuert.
3. In `Infrastructure/SimConnectFlightDataSource.cs`s `FlightDataStruct` ein passendes
   `double`-Feld an der **gleichen Position** ergänzen (muss zu Schritt 2 in Anzahl und
   Reihenfolge passen – ein Startup-Check wirft sofort eine klare Fehlermeldung, falls die
   Anzahl nicht übereinstimmt).
4. In `Infrastructure/DemoFlightDataSource.cs` einen synthetischen Wert ergänzen, damit
   der Demo-Modus die neue Property auch zeigt.
5. In der gewünschten Seite (`Application/Pages/FlightDataPage.cs` oder
   `AutopilotPage.cs`) in `BuildLines(...)` einbauen. Neue Buchstaben/Symbole brauchen
   ggf. einen weiteren Eintrag im `Font`-Dictionary in `Infrastructure/LcdDisplay.cs`
   (5x7-Bitmuster, siehe vorhandene Einträge als Vorlage).

Kommt ein Wert nicht 1:1 aus einer einzelnen SimVar (wie `FuelPercent`, berechnet aus
zwei rohen Werten), stattdessen die rohen Werte per `FlightDataVariables` befüllen und
die eigentliche Property in `FlightData.cs` als berechnete `=>`-Property definieren.

### Neue Seite hinzufügen

1. Neue Klasse in `Application/Pages/`, die `ILcdPage` implementiert (`Name` +
   `BuildLines(FlightData)` – siehe `FlightDataPage.cs`/`AutopilotPage.cs` als Vorlage).
   Das G13-Display hat nur 4 Zeilen à ca. 26 Zeichen (bei 5x7-Font + 1px Zeichenabstand).
2. In `Program.cs` (Composition Root) mit `services.AddSingleton<ILcdPage, NeueSeite>();`
   registrieren – taucht danach automatisch im Tray-Untermenü "Seite wählen" auf.
3. Optional in `Infrastructure/LcdDisplay.cs`s Konstruktor (`_buttonPages`) einer G13-
   Taste zuordnen – entweder einer neuen (`LcdButton2`/`LcdButton3` sind noch frei) oder
   einer bestehenden (dann teilen sich mehrere Seiten die Taste und wiederholtes Drücken
   blättert zwischen ihnen um). Ohne Zuordnung ist die Seite nur per Tray-Menü erreichbar.
