using System.Text.Json;

namespace PSPad.Contracts;

public sealed record JoinBudgetResponse(Guid BudgetId, IReadOnlyDictionary<string, JsonElement[]> Documents);
