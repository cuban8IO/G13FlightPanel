using System.Runtime.InteropServices;
using G13FlightPanel.Application.Pages;
using G13FlightPanel.Domain;

namespace G13FlightPanel.Infrastructure;

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

    // Die 4 Tasten unter dem G13-LCD (Bitflags aus dem Logitech LCD SDK).
    private const int LcdButton0 = 0x1;
    private const int LcdButton1 = 0x2;
    private const int LcdButton2 = 0x4;
    private const int LcdButton3 = 0x8;

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
        ['X'] = ["#...#", "#...#", ".#.#.", "..#..", ".#.#.", "#...#", "#...#"],
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
    private readonly byte[] _bitmapBuffer = new byte[MonoWidth * MonoHeight];

    private readonly IReadOnlyList<ILcdPage> _pages;
    private readonly Dictionary<int, ILcdPage[]> _buttonPages;
    private readonly Dictionary<int, int> _buttonPositions = new();
    private int _prevButtonMask;
    private ILcdPage _currentPage;

    public LcdDisplay(IPageRepository pageRepository)
    {
        _pages = pageRepository.GetAllPages();
        _currentPage = _pages[0];

        ILcdPage Of<T>() where T : ILcdPage => _pages.OfType<T>().First();

        // Button -> zugeordnete Seiten. Mehrere Eintraege = wiederholtes Druecken
        // blaettert zwischen ihnen um (jeder Button merkt sich seine eigene Position
        // unabhaengig von den anderen Buttons). Frei anpassbar/erweiterbar - eine neue
        // Seite kann einem neuen ODER einem bestehenden (geteilten) Button zugeordnet
        // werden, ohne eine andere Datei anzufassen.
        _buttonPages = new Dictionary<int, ILcdPage[]>
        {
            [LcdButton0] = [Of<FlightDataPage>()],
            [LcdButton1] = [Of<AutopilotPage>()],
            [LcdButton2] = [Of<LandingGearPage>()],
        };

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

    // Fuer das Tray-Untermenue (siehe Program.cs) - listet alle Seiten fuer die
    // Direktauswahl per Maus, unabhaengig von der physischen Button-Belegung.
    public IReadOnlyList<ILcdPage> Pages => _pages;
    public void SelectPage(int index) => _currentPage = _pages[index];

    // Vom Tray-Menu aufgerufen, wenn die Konsole neu geoeffnet wird (siehe Program.cs) -
    // ohne das wuerde ein einmal fehlgeschlagener Console.Clear()-Versuch (z.B. beim
    // allerersten Render()-Tick, bevor ueberhaupt eine Konsole existiert) den Konsolen-
    // Spiegel fuer immer abschalten, selbst nachdem spaeter eine Konsole geoeffnet wird.
    public void ResetConsoleMirror() => _consoleUsable = true;

    public void Render(FlightData data)
    {
        if (HardwareAvailable)
            HandleButtons();

        var lines = _currentPage.BuildLines(data);

        // Drei Versuche mit Cursor-Positionierung (in-place ueberschreiben ohne die
        // fruehere Ausgabe zu loeschen) haben alle das gleiche Problem gezeigt und sind
        // aufgegeben - zurueck zur einfachen, zuverlaessigen Variante: ganze Konsole
        // loeschen und neu schreiben. Start-Banner/Fehler bleiben dadurch nicht stehen,
        // aber die Anzeige selbst funktioniert wieder verlaesslich.
        //
        // Ohne sichtbare Konsole (Tray-App-Default) wirft Console.Clear() eine
        // IOException ("Handle ist ungueltig") - wird hier abgefangen und der Spiegel
        // bis zum naechsten ResetConsoleMirror()-Aufruf abgeschaltet.
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

    private void HandleButtons()
    {
        foreach (var (button, pages) in _buttonPages)
        {
            bool pressed = LogiLcdIsButtonPressed(button);
            bool wasPressed = (_prevButtonMask & button) != 0;

            // Steigende Flanke: nur beim Druecken einmal umschalten, nicht bei jedem
            // Render()-Tick erneut, solange die Taste gehalten wird.
            if (pressed && !wasPressed)
            {
                // -1 als Default: der erste Druck auf einen noch nie gedrueckten Button
                // soll die erste zugeordnete Seite zeigen (Index 0), nicht die zweite.
                int next = (_buttonPositions.GetValueOrDefault(button, -1) + 1) % pages.Length;
                _buttonPositions[button] = next;
                _currentPage = pages[next];
            }

            _prevButtonMask = pressed ? (_prevButtonMask | button) : (_prevButtonMask & ~button);
        }
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
