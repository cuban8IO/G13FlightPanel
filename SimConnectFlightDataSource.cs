#if HAVE_SIMCONNECT
using System.Runtime.InteropServices;
using Microsoft.FlightSimulator.SimConnect;

namespace G13FlightPanel;

/// Real data source, only compiled once libs/Microsoft.FlightSimulator.SimConnect.dll
/// is present (see G13FlightPanel.csproj and README.md). Uses the official managed
/// SimConnect API rather than raw P/Invoke against SimConnect.dll - Microsoft's own
/// wrapper handles all the message marshaling, which is the standard, safe way every
/// MSFS add-on (FSUIPC, MobiFlight, Air Manager, ...) talks to the sim.
public sealed class SimConnectFlightDataSource : IFlightDataSource
{
    private enum Definitions { FlightData }
    private enum Requests { FlightData }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)]
    private struct FlightDataStruct
    {
        public double IndicatedAirspeed;
        public double Altitude;
        public double VerticalSpeed;
        public double HeadingMagnetic;
        public double FlapsHandleIndex;
        public double Nav1Frequency;
        public double Nav1Obs;
        public double FuelTotalQuantity;
        public double FuelTotalCapacity;
        public double ApMaster;
        public double ApAltitudeLockVar;
        public double ApHeadingLockDir;
        public double ApAirspeedHoldVar;
        public double ApApproachHold;
        public double ApAltitudeLock;
        public double ApVerticalHold;
        public double ApHeadingLock;
        public double ApNav1Lock;
        public double ApFmaLateralMode;
        public double ApFmaVerticalMode;
    }

    private const int MaxFailedAttempts = 3;

    private SimConnect? _sc;
    private DemoFlightDataSource? _demo;
    private readonly AutoResetEvent _signal = new(false);
    private Thread? _pumpThread;
    private volatile bool _running;

    public event Action<FlightData>? DataUpdated;

    public void Start()
    {
        _running = true;
        _pumpThread = new Thread(Run) { IsBackground = true };
        _pumpThread.Start();
    }

    private void Run()
    {
        int failedAttempts = 0;
        while (_running)
        {
            try
            {
                Connect();
                failedAttempts = 0;
                while (_running && _signal.WaitOne(1000))
                    _sc?.ReceiveMessage();
            }
            catch (COMException)
            {
                // MSFS not running / SimConnect not reachable yet - retry.
                _sc?.Dispose();
                _sc = null;
                failedAttempts++;

                if (failedAttempts >= MaxFailedAttempts)
                {
                    Console.WriteLine($"MSFS nach {MaxFailedAttempts} Versuchen nicht erreichbar - wechsle in Demo-Modus.");
                    FallBackToDemo();
                    return;
                }

                Console.WriteLine($"SimConnect nicht erreichbar, naechster Versuch in 5s... ({failedAttempts}/{MaxFailedAttempts})");
                Thread.Sleep(5000);
            }
        }
    }

    private void FallBackToDemo()
    {
        _demo = new DemoFlightDataSource();
        _demo.DataUpdated += data => DataUpdated?.Invoke(data);
        _demo.Start();
    }

    private void Connect()
    {
        _sc = new SimConnect("G13 Flight Panel", IntPtr.Zero, 0, _signal, 0);

        _sc.OnRecvOpen += (_, _) => Console.WriteLine("Mit MSFS verbunden.");
        _sc.OnRecvQuit += (_, _) => Console.WriteLine("MSFS beendet, warte auf Neustart...");
        _sc.OnRecvException += (_, e) =>
            Console.WriteLine($"SimConnect-Fehler: {(SIMCONNECT_EXCEPTION)e.dwException}");

        AddVar("AIRSPEED INDICATED", "Knots");
        AddVar("INDICATED ALTITUDE", "Feet");
        AddVar("VERTICAL SPEED", "Feet per minute");
        AddVar("PLANE HEADING DEGREES MAGNETIC", "Degrees");

        // FBW A32NX legt Flap-Lever & Co. als eigene L:-Variable ab. Direktes Lesen von
        // L:-Vars per SimConnect geht seit Sim Update 10 (2022) ohne Zusatzsoftware -
        // Name ist case-sensitive und muss exakt zum FBW-Build passen. Klappt es bei dir
        // nicht (aeltere Sim-Version), alternativ das MobiFlight-WASM-Modul installieren
        // und dessen Client-Data-Area-Bridge fuer L:-Vars nutzen.
        AddVar("L:A32NX_FLAPS_HANDLE_INDEX", "Number");

        AddVar("NAV ACTIVE FREQUENCY:1", "MHz");
        AddVar("NAV OBS:1", "Degrees");

        AddVar("FUEL TOTAL QUANTITY", "Gallons");
        AddVar("FUEL TOTAL CAPACITY", "Gallons");
        AddVar("AUTOPILOT MASTER", "Bool");
        AddVar("AUTOPILOT ALTITUDE LOCK VAR", "Feet");
        AddVar("AUTOPILOT HEADING LOCK DIR", "Degrees");
        AddVar("AUTOPILOT AIRSPEED HOLD VAR", "Knots");
        AddVar("AUTOPILOT APPROACH HOLD", "Bool");
        AddVar("AUTOPILOT ALTITUDE LOCK", "Bool");
        AddVar("AUTOPILOT VERTICAL HOLD", "Bool");
        AddVar("AUTOPILOT HEADING LOCK", "Bool");
        AddVar("AUTOPILOT NAV1 LOCK", "Bool");

        // FBW-eigene FMA-Modus-Codes fuer LOC*/LOC und G/S*/G/S - die generischen
        // AUTOPILOT-*-Booleans oben kennen den Unterschied zwischen "armed/capturing"
        // (der Stern) und "captured/tracking" nicht, das ist Airbus-FMA-spezifisch.
        // Zahlencodes nach bestem Wissen (FlyByWire FmaVerticalMode/FmaLateralMode enum):
        // Lateral: 30=LOC*, 31=LOC. Vertical: 90=G/S*, 91=G/S. Koennen sich zwischen
        // FBW-Versionen aendern - falls LOC*/G/S auf dem Display falsch/blank bleiben,
        // im Sim-Devmodus (Behavior Debug) den tatsaechlichen LVar-Wert pruefen.
        AddVar("L:A32NX_FMA_LATERAL_MODE", "Number");
        AddVar("L:A32NX_FMA_VERTICAL_MODE", "Number");

        _sc.RegisterDataDefineStruct<FlightDataStruct>(Definitions.FlightData);

        _sc.OnRecvSimobjectData += (_, data) =>
        {
            if (data.dwRequestID != (uint)Requests.FlightData) return;
            var d = (FlightDataStruct)data.dwData[0];
            DataUpdated?.Invoke(new FlightData
            {
                IndicatedAirspeedKt = d.IndicatedAirspeed,
                AltitudeFt = d.Altitude,
                VerticalSpeedFpm = d.VerticalSpeed,
                HeadingDeg = d.HeadingMagnetic,
                FlapsHandleIndex = d.FlapsHandleIndex,
                Nav1FrequencyMhz = d.Nav1Frequency,
                Nav1Obs = d.Nav1Obs,
                FuelPercent = d.FuelTotalCapacity > 0 ? d.FuelTotalQuantity / d.FuelTotalCapacity * 100.0 : 0,
                ApMasterOn = d.ApMaster != 0,
                ApSelectedAltitudeFt = d.ApAltitudeLockVar,
                ApSelectedHeadingDeg = d.ApHeadingLockDir,
                ApSelectedSpeedKt = d.ApAirspeedHoldVar,
                ApApproachHoldOn = d.ApApproachHold != 0,
                ApAltitudeHoldOn = d.ApAltitudeLock != 0,
                ApVerticalSpeedHoldOn = d.ApVerticalHold != 0,
                ApHeadingHoldOn = d.ApHeadingLock != 0,
                ApNavHoldOn = d.ApNav1Lock != 0,
                ApFmaLateralMode = (int)d.ApFmaLateralMode,
                ApFmaVerticalMode = (int)d.ApFmaVerticalMode,
            });
        };

        _sc.RequestDataOnSimObject(Requests.FlightData, Definitions.FlightData,
            SimConnect.SIMCONNECT_OBJECT_ID_USER, SIMCONNECT_PERIOD.SECOND,
            SIMCONNECT_DATA_REQUEST_FLAG.DEFAULT, 0, 0, 0);
    }

    private void AddVar(string name, string units) =>
        _sc!.AddToDataDefinition(Definitions.FlightData, name, units,
            SIMCONNECT_DATATYPE.FLOAT64, 0f, SimConnect.SIMCONNECT_UNUSED);

    public void Dispose()
    {
        _running = false;
        _signal.Set();
        _pumpThread?.Join(2000);
        _sc?.Dispose();
        _demo?.Dispose();
    }
}
#endif
