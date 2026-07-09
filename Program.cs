using System.Drawing;
using System.Windows.Forms;
using G13FlightPanel;

Application.EnableVisualStyles();

try
{
    using var lcd = new LcdDisplay();

    bool forceDemo = Array.IndexOf(args, "--demo") >= 0;

    IFlightDataSource source;
#if HAVE_SIMCONNECT
    source = forceDemo ? new DemoFlightDataSource() : new SimConnectFlightDataSource();
#else
    source = new DemoFlightDataSource();
#endif

    using (source)
    {
        source.DataUpdated += lcd.Render;
        source.Start();

        using var trayIcon = new NotifyIcon
        {
            Icon = new Icon(Path.Combine(AppContext.BaseDirectory, "icon.ico")),
            Text = "G13 Flight Panel",
            Visible = true,
        };

        bool consoleVisible = false;
        var menu = new ContextMenuStrip();

        var toggleConsoleItem = menu.Items.Add("Konsole anzeigen");
        toggleConsoleItem.Click += (_, _) =>
        {
            if (consoleVisible)
            {
                NativeConsole.FreeConsole();
                toggleConsoleItem.Text = "Konsole anzeigen";
            }
            else
            {
                NativeConsole.AllocConsole();
                Console.SetOut(new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true });
                Console.WriteLine("G13 Flight Panel - Konsole geoeffnet.");
                toggleConsoleItem.Text = "Konsole ausblenden";
            }
            consoleVisible = !consoleVisible;
        };

        menu.Items.Add("Seite wechseln", null, (_, _) => lcd.TogglePage());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Beenden", null, (_, _) => Application.Exit());
        trayIcon.ContextMenuStrip = menu;

        Application.Run();
    }
}
catch (Exception e)
{
    MessageBox.Show(e.ToString(), "G13 Flight Panel - Fehler", MessageBoxButtons.OK, MessageBoxIcon.Error);
}

// Kein Konsolenfenster mehr per Default (OutputType=WinExe fuer den Tray-Betrieb) - ohne
// AllocConsole gehen Console.WriteLine-Aufrufe ins Leere statt eine Exception zu werfen,
// darum ist das ueber "Konsole anzeigen" im Tray-Menu zuschaltbar.
internal static class NativeConsole
{
    [System.Runtime.InteropServices.DllImport("kernel32.dll")]
    internal static extern bool AllocConsole();

    [System.Runtime.InteropServices.DllImport("kernel32.dll")]
    internal static extern bool FreeConsole();
}
