namespace G13FlightPanel.Domain;

public interface IFlightDataSource : IDisposable
{
    event Action<FlightData>? DataUpdated;
    void Start();
}
