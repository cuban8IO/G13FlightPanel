using G13FlightPanel.Domain;

namespace G13FlightPanel.Application;

public delegate void FlightDataAssign(FlightData data, double value);

public sealed record FlightDataVariable(string Name, string Units, FlightDataAssign Assign);

// Single source of truth fuer alle SimConnect-Variablen: treibt sowohl die
// AddToDataDefinition-Aufrufe als auch die Extraktion aus dem empfangenen Struct
// (siehe Infrastructure/SimConnectFlightDataSource.cs). Reihenfolge hier MUSS mit der
// Feldreihenfolge in FlightDataStruct uebereinstimmen - ein Startup-Check dort prueft
// zumindest die Anzahl.
public static class FlightDataVariables
{
    public static readonly IReadOnlyList<FlightDataVariable> All =
    [
        new("AIRSPEED INDICATED", "Knots", (d, v) => d.IndicatedAirspeedKt = v),
        new("INDICATED ALTITUDE", "Feet", (d, v) => d.AltitudeFt = v),
        new("VERTICAL SPEED", "Feet per minute", (d, v) => d.VerticalSpeedFpm = v),
        new("PLANE HEADING DEGREES MAGNETIC", "Degrees", (d, v) => d.HeadingDeg = v),

        // FBW A32NX legt Flap-Lever & Co. als eigene L:-Variable ab. Direktes Lesen von
        // L:-Vars per SimConnect geht seit Sim Update 10 (2022) ohne Zusatzsoftware -
        // Name ist case-sensitive und muss exakt zum FBW-Build passen. Klappt es bei dir
        // nicht (aeltere Sim-Version), alternativ das MobiFlight-WASM-Modul installieren
        // und dessen Client-Data-Area-Bridge fuer L:-Vars nutzen.
        new("L:A32NX_FLAPS_HANDLE_INDEX", "Number", (d, v) => d.FlapsHandleIndex = v),

        new("NAV ACTIVE FREQUENCY:1", "MHz", (d, v) => d.Nav1FrequencyMhz = v),
        new("NAV OBS:1", "Degrees", (d, v) => d.Nav1Obs = v),

        new("FUEL TOTAL QUANTITY", "Gallons", (d, v) => d.FuelQuantityGal = v),
        new("FUEL TOTAL CAPACITY", "Gallons", (d, v) => d.FuelCapacityGal = v),

        new("AUTOPILOT MASTER", "Bool", (d, v) => d.ApMasterOn = v != 0),
        new("AUTOPILOT ALTITUDE LOCK VAR", "Feet", (d, v) => d.ApSelectedAltitudeFt = v),
        new("AUTOPILOT HEADING LOCK DIR", "Degrees", (d, v) => d.ApSelectedHeadingDeg = v),
        new("AUTOPILOT AIRSPEED HOLD VAR", "Knots", (d, v) => d.ApSelectedSpeedKt = v),
        new("AUTOPILOT APPROACH HOLD", "Bool", (d, v) => d.ApApproachHoldOn = v != 0),
        new("AUTOPILOT ALTITUDE LOCK", "Bool", (d, v) => d.ApAltitudeHoldOn = v != 0),
        new("AUTOPILOT VERTICAL HOLD", "Bool", (d, v) => d.ApVerticalSpeedHoldOn = v != 0),
        new("AUTOPILOT HEADING LOCK", "Bool", (d, v) => d.ApHeadingHoldOn = v != 0),
        new("AUTOPILOT NAV1 LOCK", "Bool", (d, v) => d.ApNavHoldOn = v != 0),

        // FBW-eigene FMA-Modus-Codes fuer LOC*/LOC und G/S*/G/S - die generischen
        // AUTOPILOT-*-Booleans oben kennen den Unterschied zwischen "armed/capturing"
        // (der Stern) und "captured/tracking" nicht, das ist Airbus-FMA-spezifisch.
        // Zahlencodes nach bestem Wissen (FlyByWire FmaVerticalMode/FmaLateralMode enum):
        // Lateral: 30=LOC*, 31=LOC. Vertical: 90=G/S*, 91=G/S. Koennen sich zwischen
        // FBW-Versionen aendern - falls LOC*/G/S auf dem Display falsch/blank bleiben,
        // im Sim-Devmodus (Behavior Debug) den tatsaechlichen LVar-Wert pruefen.
        new("L:A32NX_FMA_LATERAL_MODE", "Number", (d, v) => d.ApFmaLateralMode = (int)v),
        new("L:A32NX_FMA_VERTICAL_MODE", "Number", (d, v) => d.ApFmaVerticalMode = (int)v),

        // Standard-MSFS-Fahrwerksmodell: nur diese 4 benannten Positionen, siehe
        // Domain/FlightData.cs fuer den Vorbehalt bei Flugzeugen mit mehr Fahrwerksbeinen.
        new("GEAR CENTER POSITION", "Percent", (d, v) => d.GearNosePercent = v),
        new("GEAR LEFT POSITION", "Percent", (d, v) => d.GearLeftPercent = v),
        new("GEAR RIGHT POSITION", "Percent", (d, v) => d.GearRightPercent = v),
        new("GEAR AUX POSITION", "Percent", (d, v) => d.GearAuxPercent = v),
    ];
}
