using G13FlightPanel.Domain;

namespace G13FlightPanel.Application;

public sealed class PageRepository : IPageRepository
{
    private readonly IReadOnlyList<ILcdPage> _pages;

    public PageRepository(IEnumerable<ILcdPage> pages) => _pages = pages.ToList();

    public IReadOnlyList<ILcdPage> GetAllPages() => _pages;
}
