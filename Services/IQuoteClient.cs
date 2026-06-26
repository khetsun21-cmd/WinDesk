using MarketTicker.Models;

namespace MarketTicker.Services;

public interface IQuoteClient
{
    Task<Quote> GetLatestAsync(MarketDefinition market, CancellationToken cancellationToken);
}
