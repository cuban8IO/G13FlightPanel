namespace G13FlightPanel;

/// Synthetic data feed so the LCD/console pipeline can be built and exercised without
/// MSFS or the SimConnect managed DLL. Used automatically whenever HAVE_SIMCONNECT is
/// not defined (see G13FlightPanel.csproj).
public sealed class DemoFlightDataSource : IFlightDataSource
{
    private readonly System.Timers.Timer _timer = new(500);
    private double _t;

    public event Action<FlightData>? DataUpdated;

    public void Start()
    {
        // Timer.Elapsed runs on a ThreadPool thread; an unhandled exception there
        // crashes the whole process instead of surfacing anywhere near Start() or
        // Main() - so the try/catch has to sit inside the handler itself.
        _timer.Elapsed += (_, _) =>
        {
            try
            {
                _t += 0.1;

                // Wechselt alle ~5s durch ein paar plausible AP-Modus-Kombinationen, damit
                // man im Demo-Modus sieht, wie sich Seite 2 bei einem Modus-Wechsel
                // veraendert, ohne dafuer MSFS zu brauchen. Die letzten 3 Phasen simulieren
                // einen ILS-Anflug (LOC-Capture, dann G/S-Capture).
                int phase = (int)_t % 7;
                bool apr = false;
                bool alt = phase is 0 or 2 or 3 or 4;
                bool vs = phase == 1;
                bool hdg = phase is 0 or 1;
                bool nav = phase == 2;
                int lateralMode = phase switch { 3 => 30, >= 4 => 31, _ => 0 }; // 30=LOC*, 31=LOC
                int verticalMode = phase switch { 5 => 90, 6 => 91, _ => 0 }; // 90=G/S*, 91=G/S

                DataUpdated?.Invoke(new FlightData
                {
                    IndicatedAirspeedKt = 250 + 10 * Math.Sin(_t),
                    AltitudeFt = 35000 + 200 * Math.Sin(_t / 2),
                    VerticalSpeedFpm = 1000 * Math.Sin(_t / 3),
                    HeadingDeg = (_t * 10) % 360,
                    FlapsHandleIndex = 0,
                    Nav1FrequencyMhz = 114.30,
                    Nav1Obs = 270,
                    FuelPercent = 75 + 20 * Math.Sin(_t / 4),
                    ApMasterOn = true,
                    ApApproachHoldOn = apr,
                    ApAltitudeHoldOn = alt,
                    ApVerticalSpeedHoldOn = vs,
                    ApHeadingHoldOn = hdg,
                    ApNavHoldOn = nav,
                    ApSelectedAltitudeFt = 30000 + 6000 * Math.Sin(_t / 10),
                    ApSelectedHeadingDeg = (_t * 15) % 360,
                    ApSelectedSpeedKt = 250 + 20 * Math.Sin(_t / 7),
                    ApFmaLateralMode = lateralMode,
                    ApFmaVerticalMode = verticalMode,
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        };
        _timer.Start();
    }

    public void Dispose() => _timer.Dispose();
}
