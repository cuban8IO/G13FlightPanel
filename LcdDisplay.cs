using System.Runtime.InteropServices;

namespace G13FlightPanel;

/// P/Invoke wrapper around the flat "Logitech Gaming LCD SDK" (LogitechLcd.dll) for the
/// G13/G15/G510 mono LCD. Renders text itself with a hand-drawn 5x7 pixel font (each
/// glyph a fixed on/off bitmap, no anti-aliasing) and pushes the result via
/// LogiLcdMonoSetBackground. Two earlier approaches didn't work out at this resolution
/// (160x43, 4 lines - ~10px per line): the SDK's own LogiLcdMonoSetText uses a built-in
/// font that isn't monospace, so numbers never lined up; a self-rendered GDI+ TrueType
/// font (e.g. Consolas) is too small at that pixel height for hinting to produce solid
/// strokes and just looks like scattered dots. A hand-rolled bitmap font sidesteps both -
/// every glyph is exactly 5x7 pixels, drawn as-is with no smoothing. Falls back to
/// console-only output when the DLL/hardware isn't present, so this runs fine on a
/// machine without LGS or a G13 attached.
public sealed class LcdDisplay : IDisposable
{
    private const string DllName = "LogitechLcd.dll";
    private const int LcdTypeMono = 0x1;
    private const int MonoWidth = 160;
    private const int MonoHeight = 43;
    private const int PageCount = 2;

    // Die 4 Tasten unter dem G13-LCD (Bitflags aus dem Logitech LCD SDK).
    private const int LcdButton0 = 0x1;

    private const int GlyphWidth = 5;
    private const int GlyphHeight = 7;
    private const int CharGap = 1;
    private const int LinePitch = 9;
    private const int TopMargin = 4;
    private const int LeftMargin = 1;

    // If the display comes out inverted (dark background, light text) on your hardware,
    // swap these two.
    private const byte PixelOn = 255;
    private const byte PixelOff = 0;

    // 5x7 bitmap font, one glyph per character actually used on the panel. Each glyph is
    // 7 rows of 5 characters, '#' = lit pixel, '.' = unlit.
    private static readonly Dictionary<char, string[]> Font = new()
    {
        ['0'] = [".###.", "#...#", "#..##", "#.#.#", "##..#", "#...#", ".###."],
        ['1'] = ["..#..", ".##..", "..#..", "..#..", "..#..", "..#..", ".###."],
        ['2'] = [".###.", "#...#", "....#", "...#.", "..#..", ".#...", "#####"],
        ['3'] = [".###.", "#...#", "....#", "..##.", "....#", "#...#", ".###."],
        ['4'] = ["...#.", "..##.", ".#.#.", "#..#.", "#####", "...#.", "...#."],
        ['5'] = ["#####", "#....", "####.", "....#", "....#", "#...#", ".###."],
        ['6'] = ["..##.", ".#...", "#....", "####.", "#...#", "#...#", ".###."],
        ['7'] = ["#####", "....#", "...#.", "..#..", ".#...", ".#...", ".#..."],
        ['8'] = [".###.", "#...#", "#...#", ".###.", "#...#", "#...#", ".###."],
        ['9'] = [".###.", "#...#", "#...#", ".####", "....#", "...#.", ".##.."],
        ['A'] = ["..#..", ".#.#.", "#...#", "#...#", "#####", "#...#", "#...#"],
        ['B'] = ["####.", "#...#", "#...#", "####.", "#...#", "#...#", "####."],
        ['C'] = [".###.", "#...#", "#....", "#....", "#....", "#...#", ".###."],
        ['D'] = ["####.", "#...#", "#...#", "#...#", "#...#", "#...#", "####."],
        ['E'] = ["#####", "#....", "#....", "####.", "#....", "#....", "#####"],
        ['F'] = ["#####", "#....", "#....", "####.", "#....", "#....", "#...."],
        ['G'] = [".###.", "#...#", "#....", "#.###", "#...#", "#...#", ".###."],
        ['H'] = ["#...#", "#...#", "#...#", "#####", "#...#", "#...#", "#...#"],
        ['I'] = ["#####", "..#..", "..#..", "..#..", "..#..", "..#..", "#####"],
        ['K'] = ["#...#", "#..#.", "#.#..", "##...", "#.#..", "#..#.", "#...#"],
        ['L'] = ["#....", "#....", "#....", "#....", "#....", "#....", "#####"],
        ['M'] = ["#...#", "##.##", "#.#.#", "#...#", "#...#", "#...#", "#...#"],
        ['N'] = ["#...#", "##..#", "#.#.#", "#..##", "#...#", "#...#", "#...#"],
        ['O'] = [".###.", "#...#", "#...#", "#...#", "#...#", "#...#", ".###."],
        ['P'] = ["####.", "#...#", "#...#", "####.", "#....", "#....", "#...."],
        ['R'] = ["####.", "#...#", "#...#", "####.", "#.#..", "#..#.", "#...#"],
        ['S'] = [".####", "#....", "#....", ".###.", "....#", "....#", "####."],
        ['T'] = ["#####", "..#..", "..#..", "..#..", "..#..", "..#..", "..#.."],
        ['U'] = ["#...#", "#...#", "#...#", "#...#", "#...#", "#...#", ".###."],
        ['V'] = ["#...#", "#...#", "#...#", "#...#", "#...#", ".#.#.", "..#.."],
        ['+'] = [".....", "..#..", "..#..", "#####", "..#..", "..#..", "....."],
        ['-'] = [".....", ".....", ".....", "#####", ".....", ".....", "....."],
        ['*'] = [".....", "#...#", ".#.#.", "..#..", ".#.#.", "#...#", "....."],
        ['.'] = [".....", ".....", ".....", ".....", ".....", ".##..", ".##.."],
        ['/'] = ["....#", "....#", "...#.", "..#..", ".#...", "#....", "#...."],
        [' '] = [".....", ".....", ".....", ".....", ".....", ".....", "....."],
    };

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "LogiLcdInit", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool LogiLcdInit(string friendlyName, int lcdType);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "LogiLcdIsConnected", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool LogiLcdIsConnected(int lcdType);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "LogiLcdIsButtonPressed", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool LogiLcdIsButtonPressed(int button);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "LogiLcdMonoSetBackground", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool LogiLcdMonoSetBackground(byte[] monoBitmap);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "LogiLcdUpdate", CharSet = CharSet.Unicode)]
    private static extern void LogiLcdUpdate();

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "LogiLcdShutdown", CharSet = CharSet.Unicode)]
    private static extern void LogiLcdShutdown();

    public bool HardwareAvailable { get; }
    private bool _consoleUsable = true;
    private int _page;
    private bool _button0WasPressed;
    private readonly byte[] _bitmapBuffer = new byte[MonoWidth * MonoHeight];

    public LcdDisplay()
    {
        try
        {
            HardwareAvailable = LogiLcdInit("G13 Flight Panel", LcdTypeMono) && LogiLcdIsConnected(LcdTypeMono);
        }
        catch (DllNotFoundException)
        {
            HardwareAvailable = false;
        }
        catch (BadImageFormatException)
        {
            // Process bitness (see <PlatformTarget> in the .csproj) doesn't match the
            // installed LogitechLcd.dll - flip x86/x64 there.
            HardwareAvailable = false;
        }

        Console.WriteLine(HardwareAvailable
            ? "G13-LCD gefunden - Ausgabe auf Hardware + Konsole."
            : "Kein G13/LogitechLcd.dll gefunden - Ausgabe nur auf der Konsole.");
    }

    public void Render(FlightData data)
    {
        if (HardwareAvailable)
        {
            // Steigende Flanke: nur beim Druecken einmal umschalten, nicht bei jedem
            // Render()-Tick erneut, solange die Taste gehalten wird.
            bool button0Pressed = LogiLcdIsButtonPressed(LcdButton0);
            if (button0Pressed && !_button0WasPressed)
                _page = (_page + 1) % PageCount;
            _button0WasPressed = button0Pressed;
        }

        var lines = _page == 0 ? BuildFlightPage(data) : BuildAutopilotPage(data);

        // Drei Versuche mit Cursor-Positionierung (in-place ueberschreiben ohne die
        // fruehere Ausgabe zu loeschen) haben alle das gleiche Problem gezeigt und sind
        // aufgegeben - zurueck zur einfachen, zuverlaessigen Variante: ganze Konsole
        // loeschen und neu schreiben. Start-Banner/Fehler bleiben dadurch nicht stehen,
        // aber die Anzeige selbst funktioniert wieder verlaesslich.
        //
        // VS Code's Debug Console (and any other redirected/non-terminal host) has no
        // real console buffer - Console.Clear() can throw there with varying exception
        // types depending on host. Try once; if it fails, stop retrying every tick and
        // just skip the console mirror from then on (LCD output below is unaffected).
        if (_consoleUsable)
        {
            try
            {
                Console.Clear();
                foreach (var line in lines)
                    Console.WriteLine(line);
            }
            catch (Exception)
            {
                _consoleUsable = false;
            }
        }

        if (!HardwareAvailable) return;

        RenderToBitmap(lines);
        LogiLcdMonoSetBackground(_bitmapBuffer);
        LogiLcdUpdate();
    }

    private static string[] BuildFlightPage(FlightData data)
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

    private static string[] BuildAutopilotPage(FlightData data)
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

    private void RenderToBitmap(string[] lines)
    {
        Array.Fill(_bitmapBuffer, PixelOff);

        for (int li = 0; li < lines.Length; li++)
        {
            int py = TopMargin + li * LinePitch;
            int px = LeftMargin;
            foreach (char c in lines[li])
            {
                DrawChar(c, px, py);
                px += GlyphWidth + CharGap;
            }
        }
    }

    private void DrawChar(char c, int originX, int originY)
    {
        if (!Font.TryGetValue(char.ToUpperInvariant(c), out var glyph)) return;

        for (int y = 0; y < GlyphHeight; y++)
        {
            for (int x = 0; x < GlyphWidth; x++)
            {
                if (glyph[y][x] != '#') continue;
                int bx = originX + x, by = originY + y;
                if ((uint)bx >= MonoWidth || (uint)by >= MonoHeight) continue;
                _bitmapBuffer[by * MonoWidth + bx] = PixelOn;
            }
        }
    }

    public void Dispose()
    {
        if (HardwareAvailable)
            LogiLcdShutdown();
    }
}
