using System.Text.Json;
using Microsoft.JSInterop;
using PSPad.Module.Tasks.Lists;
using PSPad.Module.Tasks.Tasks;

namespace PSPad.App.State;

public sealed class ListScreenState(IJSRuntime js)
{
    public const string KeyPrefix = "pspad.listview.";

    static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<ListScreenChoice> LoadAsync(Guid listId)
    {
        try
        {
            var raw = await js.InvokeAsync<string?>("localStorage.getItem", KeyPrefix + listId);
            return raw is null ? ListScreenChoice.Default : From(JsonSerializer.Deserialize<StoredListScreen>(raw, Json));
        }
        catch (Exception exception) when (exception is JSException or JsonException)
        {
            return ListScreenChoice.Default;
        }
    }

    public async Task SaveAsync(Guid listId, ListScreenChoice choice)
    {
        try
        {
            if (choice.IsDefault)
            {
                await js.InvokeVoidAsync("localStorage.removeItem", KeyPrefix + listId);
                return;
            }

            await js.InvokeVoidAsync("localStorage.setItem", KeyPrefix + listId, JsonSerializer.Serialize(To(choice), Json));
        }
        catch (JSException)
        {
        }
    }

    static ListScreenChoice From(StoredListScreen? stored)
    {
        if (stored is null)
        {
            return ListScreenChoice.Default;
        }

        var filter = new ListFilter(
            Parsed<WhenBucket>(stored.When),
            Parsed<Priority>(stored.Priority),
            stored.Starred,
            stored.HasGoal,
            (stored.Goals ?? []).ToHashSet());
        var sort = TryName<ListOrder>(stored.Order) is { } order
            ? new ListSort(order, stored.Descending)
            : ListSort.Default;
        var view = TryName<ListScreenView>(stored.View) ?? ListScreenView.List;

        return new ListScreenChoice(filter, sort, view);
    }

    static StoredListScreen To(ListScreenChoice choice) => new(
        [.. choice.Filter.When.Select(bucket => bucket.ToString())],
        [.. choice.Filter.Priorities.Select(priority => priority.ToString())],
        choice.Filter.Starred,
        choice.Filter.HasGoal,
        [.. choice.Filter.GoalIds],
        choice.Sort.Order.ToString(),
        choice.Sort.Descending,
        choice.View.ToString());

    static HashSet<T> Parsed<T>(string[]? names) where T : struct, Enum =>
        (names ?? []).Select(TryName<T>).OfType<T>().ToHashSet();

    static T? TryName<T>(string? name) where T : struct, Enum =>
        Enum.GetNames<T>().Contains(name) ? Enum.Parse<T>(name!) : null;
}
