using WinDesk.Models;

namespace WinDesk.Services;

public interface IQuoteClient
{
    Task<Quote> GetLatestAsync(MarketDefinition market, CancellationToken cancellationToken);
}
