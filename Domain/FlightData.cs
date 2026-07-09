namespace G13FlightPanel.Domain;

// Nicht jede Property ist 1:1 aus FlightDataVariables gespeist - manche sind berechnet
// (z.B. FuelPercent aus FuelQuantityGal/FuelCapacityGal). Siehe Application/FlightDataVariables.cs.
public sealed class FlightData
{
    public double IndicatedAirspeedKt { get; set; }
    public double AltitudeFt { get; set; }
    public double VerticalSpeedFpm { get; set; }
    public double HeadingDeg { get; set; }
    public double FlapsHandleIndex { get; set; }
    public double Nav1FrequencyMhz { get; set; }
    public double Nav1Obs { get; set; }

    public double FuelQuantityGal { get; set; }
    public double FuelCapacityGal { get; set; }
    public double FuelPercent => FuelCapacityGal > 0 ? FuelQuantityGal / FuelCapacityGal * 100 : 0;

    public bool ApMasterOn { get; set; }
    public bool ApApproachHoldOn { get; set; }
    public bool ApAltitudeHoldOn { get; set; }
    public bool ApVerticalSpeedHoldOn { get; set; }
    public bool ApHeadingHoldOn { get; set; }
    public bool ApNavHoldOn { get; set; }
    public double ApSelectedAltitudeFt { get; set; }
    public double ApSelectedHeadingDeg { get; set; }
    public double ApSelectedSpeedKt { get; set; }

    // FBW A32NX-spezifische FMA-Modus-Codes (LOC*/LOC, G/S*/G/S) - siehe
    // SimConnectFlightDataSource fuer die genutzten LVar-Namen und den Zahlencode-
    // Vorbehalt. 0 = kein bekannter Modus / LVar nicht vorhanden.
    public int ApFmaLateralMode { get; set; }
    public int ApFmaVerticalMode { get; set; }
}
