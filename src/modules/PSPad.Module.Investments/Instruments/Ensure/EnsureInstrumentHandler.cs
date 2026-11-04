using PSPad.Abstractions;
using PSPad.Module.Investments.Portfolio;

namespace PSPad.Module.Investments.Instruments;

public sealed class EnsureInstrumentHandler(
    IDocumentStore<Instrument> instruments, IDocumentStore<InvestmentPortfolio> portfolios, IUnitOfWork work,
    IClock clock)
    : ICommandHandler<EnsureInstrument>
{
    public async Task<CommandResult> HandleAsync(EnsureInstrument command, CancellationToken ct)
    {
        var existing = await instruments.LoadAsync(Instrument.IdFor(command.UserId, command.Ticker), ct);

        try
        {
            var at = clock.UtcNow;
            var instrumentEvents = Instrument.Decide(existing, command, at);
            if (instrumentEvents.Count > 0)
            {
                var portfolio = await portfolios.LoadAsync(InvestmentPortfolio.IdFor(command.UserId), ct);
                var portfolioEvents = InvestmentPortfolio.Decide(portfolio, command, at);

                var instrument = new Instrument();
                instrument.ApplyAll(instrumentEvents);
                work.Stage(instrument, instrumentEvents);

                if (portfolioEvents.Count > 0)
                {
                    var counted = portfolio ?? new InvestmentPortfolio();
                    counted.ApplyAll(portfolioEvents);
                    work.Stage(counted, portfolioEvents);
                }
            }

            await work.CommitAsync(command.CommandId, command.UserId, ct);
            return CommandResult.Ok();
        }
        catch (DomainRejectedException rejection)
        {
            return CommandResult.Rejected(rejection);
        }
    }
}
