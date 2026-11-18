namespace PSPad.Api.Tests.Reminders;

public sealed record PushRequest(Uri Uri, IReadOnlyDictionary<string, string> Headers, byte[] Body);
