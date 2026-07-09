#if HAVE_SIMCONNECT
using System.Runtime.InteropServices;
using Microsoft.FlightSimulator.SimConnect;
using G13FlightPanel.Application;
using G13FlightPanel.Domain;

namespace G13FlightPanel.Infrastructure;

/// Real data source, only compiled once libs/Microsoft.FlightSimulator.SimConnect.dll
/// is present (see G13FlightPanel.csproj and README.md). Uses the official managed
/// SimConnect API rather than raw P/Invoke against SimConnect.dll - Microsoft's own
/// wrapper handles all the message marshaling, which is the standard, safe way every
/// MSFS add-on (FSUIPC, MobiFlight, Air Manager, ...) talks to the sim.
public sealed class SimConnectFlightDataSource : IFlightDataSource
{
    private enum Definitions { FlightData }
    private enum Requests { FlightData }

    // Feldreihenfolge MUSS mit FlightDataVariables.All uebereinstimmen (siehe
    // Application/FlightDataVariables.cs) - der Startup-Check unten prueft zumindest
    // die Anzahl. RegisterDataDefineStruct<T>() reflektiert ueber diese benannten
    // Felder zur Registrierungszeit und gleicht sie 1:1 gegen die AddToDataDefinition-
    // Reihenfolge ab - das ist der Grund, warum dieses Struct (statt z.B. eines
    // dynamischen double[]-Feldes) so bleibt: es ist die einzige Form, von der bekannt
    // ist, dass der SimConnect-Wrapper sie korrekt marshalt.
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)]
    internal struct FlightDataStruct
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

    // Faellt sofort mit klarer Meldung auf statt spaeter still falsche/verschobene Werte
    // aufs LCD zu bringen, falls FlightDataVariables.All und FlightDataStruct auseinander-
    // laufen (z.B. eine neue Variable wurde nur an einer der beiden Stellen ergaenzt).
    static SimConnectFlightDataSource()
    {
        int fieldCount = typeof(FlightDataStruct).GetFields().Length;
        int variableCount = FlightDataVariables.All.Count;
        if (fieldCount != variableCount)
            throw new InvalidOperationException(
                $"FlightDataStruct hat {fieldCount} Felder, aber FlightDataVariables.All hat " +
                $"{variableCount} Eintraege - beides muss in Anzahl und Reihenfolge uebereinstimmen.");
    }

    private const int MaxFailedAttempts = 3;
    private const int RetryDelayMs = 5000;
    private const int ReconnectWhileDemoDelayMs = 15000;

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
        bool usingDemo = false;

        while (_running)
        {
            try
            {
                Connect();
                failedAttempts = 0;

                if (usingDemo)
                {
                    Console.WriteLine("MSFS wieder erreichbar - wechsle zurueck von Demo-Modus.");
                    StopDemo();
                    usingDemo = false;
                }

                while (_running && _signal.WaitOne(1000))
                    _sc?.ReceiveMessage();
            }
            catch (COMException)
            {
                // MSFS not running / SimConnect not reachable yet - retry. Once in demo
                // mode, keep trying in the background (slower interval, no more urgency)
                // so real data comes back automatically once MSFS is up, without needing
                // to restart the app.
                _sc?.Dispose();
                _sc = null;

                if (usingDemo)
                {
                    // _signal statt Thread.Sleep: Dispose() kann so per _signal.Set()
                    // sofort aufwecken, statt bis zu 15s auf den Shutdown zu warten.
                    _signal.WaitOne(ReconnectWhileDemoDelayMs);
                    continue;
                }

                failedAttempts++;

                if (failedAttempts >= MaxFailedAttempts)
                {
                    Console.WriteLine($"MSFS nach {MaxFailedAttempts} Versuchen nicht erreichbar - wechsle in Demo-Modus.");
                    StartDemo();
                    usingDemo = true;
                    continue;
                }

                Console.WriteLine($"SimConnect nicht erreichbar, naechster Versuch in 5s... ({failedAttempts}/{MaxFailedAttempts})");
                _signal.WaitOne(RetryDelayMs);
            }
        }

        StopDemo();
    }

    private void StartDemo()
    {
        _demo = new DemoFlightDataSource();
        _demo.DataUpdated += RelayDemoData;
        _demo.Start();
    }

    private void StopDemo()
    {
        if (_demo is null) return;
        _demo.DataUpdated -= RelayDemoData;
        _demo.Dispose();
        _demo = null;
    }

    private void RelayDemoData(FlightData data) => DataUpdated?.Invoke(data);

    private void Connect()
    {
        _sc = new SimConnect("G13 Flight Panel", IntPtr.Zero, 0, _signal, 0);

        _sc.OnRecvOpen += (_, _) => Console.WriteLine("Mit MSFS verbunden.");
        _sc.OnRecvQuit += (_, _) => Console.WriteLine("MSFS beendet, warte auf Neustart...");
        _sc.OnRecvException += (_, e) =>
            Console.WriteLine($"SimConnect-Fehler: {(SIMCONNECT_EXCEPTION)e.dwException}");

        foreach (var v in FlightDataVariables.All)
            _sc.AddToDataDefinition(Definitions.FlightData, v.Name, v.Units,
                SIMCONNECT_DATATYPE.FLOAT64, 0f, SimConnect.SIMCONNECT_UNUSED);

        _sc.RegisterDataDefineStruct<FlightDataStruct>(Definitions.FlightData);

        _sc.OnRecvSimobjectData += (_, data) =>
        {
            if (data.dwRequestID != (uint)Requests.FlightData) return;
            var raw = (FlightDataStruct)data.dwData[0];
            DataUpdated?.Invoke(ExtractFlightData(raw));
        };

        _sc.RequestDataOnSimObject(Requests.FlightData, Definitions.FlightData,
            SimConnect.SIMCONNECT_OBJECT_ID_USER, SIMCONNECT_PERIOD.SECOND,
            SIMCONNECT_DATA_REQUEST_FLAG.DEFAULT, 0, 0, 0);
    }

    // Reflektiert ueber das bereits vom SimConnect-Wrapper korrekt befuellte Struct
    // (passiert NACH dem Marshaling, also ganz normale, ungefaehrliche .NET-Reflection)
    // und zippt die Werte in Deklarationsreihenfolge gegen FlightDataVariables.All.
    // internal, damit ein Wegwerf-Check (siehe README/Verifikation) sie direkt aufrufen kann.
    internal static FlightData ExtractFlightData(FlightDataStruct raw)
    {
        var fields = typeof(FlightDataStruct).GetFields();
        var data = new FlightData();
        for (int i = 0; i < FlightDataVariables.All.Count; i++)
        {
            double value = (double)fields[i].GetValue(raw)!;
            FlightDataVariables.All[i].Assign(data, value);
        }
        return data;
    }

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
