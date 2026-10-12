namespace PSPad.Api.Rates;

public sealed class NbpUnavailableException(string message, Exception? inner = null) : Exception(message, inner);
