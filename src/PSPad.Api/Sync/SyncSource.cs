namespace PSPad.Api.Sync;

sealed record SyncSource(string Collection, IReadOnlyList<SyncRow> Rows, bool Full, long LastSeq);
