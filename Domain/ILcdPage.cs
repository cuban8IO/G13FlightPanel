namespace G13FlightPanel.Domain;

public interface ILcdPage
{
    string Name { get; }
    string[] BuildLines(FlightData data);
}
