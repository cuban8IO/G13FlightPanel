namespace G13FlightPanel.Domain;

public interface IPageRepository
{
    IReadOnlyList<ILcdPage> GetAllPages();
}
