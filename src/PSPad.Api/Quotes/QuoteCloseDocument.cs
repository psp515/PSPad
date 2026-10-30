namespace PSPad.Api.Quotes;

public sealed class QuoteCloseDocument
{
    public DateOnly Date { get; set; }

    public decimal Close { get; set; }
}
