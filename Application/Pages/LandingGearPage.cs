using G13FlightPanel.Domain;

namespace G13FlightPanel.Application.Pages;

public sealed class LandingGearPage : ILcdPage
{
    public string Name => "Fahrwerk";

    public string[] BuildLines(FlightData data)
    {
        // Gesamtstatus aus Bug/Links/Rechts (die 3 immer vorhandenen Beine) - Aux
        // bewusst aussen vor, da es bei den meisten Flugzeugen (z.B. A320) dauerhaft 0
        // bleibt und den Status sonst faelschlich auf "TRANSIT" ziehen wuerde.
        string status = data.GearNosePercent < 1 && data.GearLeftPercent < 1 && data.GearRightPercent < 1 ? "UP"
            : data.GearNosePercent > 99 && data.GearLeftPercent > 99 && data.GearRightPercent > 99 ? "DN"
            : "TRANSIT";

        return
        [
            $"NOSE {data.GearNosePercent:000}",
            $"L {data.GearLeftPercent:000}    R {data.GearRightPercent:000}",
            $"AUX {data.GearAuxPercent:000}",
            $"GEAR {status}",
        ];
    }
}
