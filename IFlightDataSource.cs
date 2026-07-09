namespace G13FlightPanel;

public interface IFlightDataSource : IDisposable
{
    event Action<FlightData>? DataUpdated;
    void Start();
}
