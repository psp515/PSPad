using System.Text.Json;

namespace PSPad.Api.Sync;

sealed record SyncRow(Guid Id, long Seq, JsonElement Json, bool Whole);
