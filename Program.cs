using G13FlightPanel;

try
{
    Console.WriteLine("G13 Flight Panel startet...");

    using var lcd = new LcdDisplay();

    bool forceDemo = Array.IndexOf(args, "--demo") >= 0;

    IFlightDataSource source;
#if HAVE_SIMCONNECT
    if (forceDemo)
    {
        source = new DemoFlightDataSource();
        Console.WriteLine("Modus: Demo (--demo erzwungen)");
    }
    else
    {
        source = new SimConnectFlightDataSource();
        Console.WriteLine("Modus: SimConnect (verbindet mit MSFS)");
    }
#else
    source = new DemoFlightDataSource();
    Console.WriteLine("Modus: Demo (keine libs/Microsoft.FlightSimulator.SimConnect.dll gefunden, siehe README.md)");
#endif

    using (source)
    {
        source.DataUpdated += lcd.Render;
        source.Start();

        Console.WriteLine("Beenden mit Strg+C.");
        var exit = new ManualResetEventSlim(false);
        Console.CancelKeyPress += (_, e) => { e.Cancel = true; exit.Set(); };
        exit.Wait();
    }
}
catch (Exception e)
{
    Console.WriteLine(e.ToString());
}
