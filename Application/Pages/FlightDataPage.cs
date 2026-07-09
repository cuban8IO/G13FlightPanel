using G13FlightPanel.Domain;

namespace G13FlightPanel.Application.Pages;

public sealed class FlightDataPage : ILcdPage
{
    public string Name => "Flugdaten";

    public string[] BuildLines(FlightData data)
    {
        const int ColWidth = 13;
        string leftIas = $"IAS {data.IndicatedAirspeedKt:000}KT".PadRight(ColWidth);
        string leftAlt = $"ALT {data.AltitudeFt:00000}FT".PadRight(ColWidth);
        string leftNav = $"NAV {data.Nav1FrequencyMhz:000.00}".PadRight(ColWidth);
        string leftFlp = $"FLP {data.FlapsHandleIndex:0}/4".PadRight(ColWidth);

        return
        [
            leftIas + $"HDG {data.HeadingDeg:000}",
            leftAlt + $"VS {data.VerticalSpeedFpm:+0000;-0000}",
            leftFlp + $"FUEL {data.FuelPercent:000}",
            leftNav + $"OBS {data.Nav1Obs:000}",
        ];
    }
}
