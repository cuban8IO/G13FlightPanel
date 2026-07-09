using G13FlightPanel.Domain;

namespace G13FlightPanel.Application.Pages;

public sealed class AutopilotPage : ILcdPage
{
    public string Name => "Autopilot";

    public string[] BuildLines(FlightData data)
    {
        // Aktiver Vertikal-/Lateral-Modus statt nur des APPR-Knopfstatus - zeigt, welcher
        // Modus der AP gerade tatsaechlich verfolgt (wie die FMA im echten Cockpit).
        // Bewusst als eigenes "MODE"-Feld getrennt von den "SEL"-Zeilen darunter: SEL ist
        // der eingestellte Zielwert, MODE ist der gerade aktiv verfolgte Modus - zwei
        // unterschiedliche Dinge.
        //
        // FBW-FMA-Codes (LOC*/LOC, G/S*/G/S) haben Vorrang vor den generischen Booleans:
        // die kennen "armed/capturing" (der Stern) nicht, nur "an/aus". Steht der FMA-Code
        // auf 0 (LVar nicht vorhanden/falscher Zahlencode), faellt es automatisch auf die
        // generische Anzeige zurueck statt blank/falsch zu bleiben.
        string vertMode = data.ApFmaVerticalMode == 90 ? "G/S*"
            : data.ApFmaVerticalMode == 91 ? "G/S"
            : data.ApApproachHoldOn ? "APR"
            : data.ApAltitudeHoldOn ? "ALT"
            : data.ApVerticalSpeedHoldOn ? "VS"
            : "---";
        string latMode = data.ApFmaLateralMode == 30 ? "LOC*"
            : data.ApFmaLateralMode == 31 ? "LOC"
            : data.ApNavHoldOn ? "NAV"
            : data.ApHeadingHoldOn ? "HDG"
            : "---";

        const int ColWidth = 12;
        string leftAp = $"AP {(data.ApMasterOn ? "ON" : "OFF")}".PadRight(ColWidth);

        return
        [
            leftAp + $"MODE {vertMode}/{latMode}",
            $"ALT SEL {data.ApSelectedAltitudeFt:00000}FT",
            $"HDG SEL {data.ApSelectedHeadingDeg:000}",
            $"SPD SEL {data.ApSelectedSpeedKt:000}KT",
        ];
    }
}
