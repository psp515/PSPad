namespace PSPad.Api.Quotes;

public sealed class QuoteUnavailableException(string message, Exception? inner = null) : Exception(message, inner);
