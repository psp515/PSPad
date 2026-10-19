namespace PSPad.App.Api;

public sealed record SharingRejection(SharingFailure Failure, string? Detail = null);
