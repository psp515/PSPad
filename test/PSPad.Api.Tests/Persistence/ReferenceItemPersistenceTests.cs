using MongoDB.Bson;
using MongoDB.Driver;
using PSPad.Abstractions;
using PSPad.Infrastructure.Mongo;
using PSPad.Module.Tasks.Lists;
using PSPad.Module.Tasks.References;
using PSPad.Module.Tasks.Tasks;
using PSPad.TestInfrastructure;

namespace PSPad.Api.Tests.Persistence;

[IntegrationTest]
[Collection(MongoCollection.Name)]
public class ReferenceItemPersistenceTests(MongoFixture fixture)
{
    [Fact]
    public async Task AReferenceItemWithFieldsRoundTrips()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        var user = Guid.NewGuid();
        var context = TestContext.For(fixture);
        var listId = Guid.NewGuid();

        var item = new ReferenceItem();
        item.ApplyAll(ReferenceItem.Decide(
            null, new CreateReferenceItem(Guid.NewGuid(), user, Guid.NewGuid(), listId, "PLA Black", 0),
            DateTimeOffset.UtcNow));
        var colourField = Guid.NewGuid();
        var amountField = Guid.NewGuid();
        var pathField = Guid.NewGuid();
        item.ApplyAll(ReferenceItem.Decide(
            item, new AddReferenceField(Guid.NewGuid(), user, item.Id, colourField, "Colour", "Black", null),
            DateTimeOffset.UtcNow));
        item.ApplyAll(ReferenceItem.Decide(
            item, new AddReferenceField(Guid.NewGuid(), user, item.Id, amountField, "Left", "350 g", "quantity"),
            DateTimeOffset.UtcNow));
        item.ApplyAll(ReferenceItem.Decide(
            item, new AddReferenceField(Guid.NewGuid(), user, item.Id, pathField, "Folder", "/mnt/spools", "path"),
            DateTimeOffset.UtcNow));
        item.ApplyAll(ReferenceItem.Decide(
            item, new SetReferenceItemDescription(Guid.NewGuid(), user, item.Id, "Dry 4h at 50 °C"),
            DateTimeOffset.UtcNow));
        item.ApplyAll(ReferenceItem.Decide(
            item, new StarReferenceItem(Guid.NewGuid(), user, item.Id, true), DateTimeOffset.UtcNow));

        await context.Collection<ReferenceItem>().InsertOneAsync(item, cancellationToken: ct);
        var loaded = await new MongoDocumentStore<ReferenceItem>(context).LoadAsync(item.Id, ct);

        Assert.NotNull(loaded);
        Assert.Equal("PLA Black", loaded.Name);
        Assert.Equal("Dry 4h at 50 °C", loaded.Description);
        Assert.True(loaded.Starred);
        Assert.Equal(3, loaded.Fields.Count);
        Assert.Equal(["Colour", "Left", "Folder"], loaded.Fields.Select(field => field.Label));
        Assert.Equal(["Black", "350 g", "/mnt/spools"], loaded.Fields.Select(field => field.Value));
        Assert.Equal([null, "quantity", "path"], loaded.Fields.Select(field => field.Display));
        Assert.Equal([0, 1, 2], loaded.Fields.Select(field => field.Position));
    }

    [Fact]
    public async Task ATaskListKeepsItsKind()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        var user = Guid.NewGuid();
        var context = TestContext.For(fixture);

        var list = new TaskList();
        list.ApplyAll(TaskList.Decide(
            null, new CreateTaskList(Guid.NewGuid(), user, Guid.NewGuid(), Guid.NewGuid(), "Filaments", 0, ListKind.Reference),
            DateTimeOffset.UtcNow));

        await context.Collection<TaskList>().InsertOneAsync(list, cancellationToken: ct);
        var loaded = await new MongoDocumentStore<TaskList>(context).LoadAsync(list.Id, ct);

        Assert.Equal(ListKind.Reference, loaded!.Kind);
    }

    [Fact]
    public async Task ATaskListStoredWithoutAKindLoadsAsTasks()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        var user = Guid.NewGuid();
        var id = Guid.NewGuid();
        var context = TestContext.For(fixture);

        await context.Collection<BsonDocument>("tasklists").InsertOneAsync(new BsonDocument
        {
            ["_id"] = new BsonBinaryData(id, GuidRepresentation.Standard),
            ["userId"] = new BsonBinaryData(user, GuidRepresentation.Standard),
            ["areaId"] = new BsonBinaryData(Guid.NewGuid(), GuidRepresentation.Standard),
            ["name"] = "Chores",
            ["position"] = 0,
            ["version"] = 1,
            ["deleted"] = false,
            ["seq"] = 1L
        }, cancellationToken: ct);

        var loaded = await new MongoDocumentStore<TaskList>(context).LoadAsync(id, ct);

        Assert.Equal(ListKind.Tasks, loaded!.Kind);
    }

    [Fact]
    public async Task ATaskDescriptionRoundTrips()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        var user = Guid.NewGuid();
        var context = TestContext.For(fixture);
        const string markdown = "# Title\n\nSome `code` here.\nSecond line.";

        var task = new TodoTask();
        task.ApplyAll(TodoTask.Decide(
            null, new CreateTask(Guid.NewGuid(), user, Guid.NewGuid(), Guid.NewGuid(), "Read a book"),
            DateTimeOffset.UtcNow));
        task.ApplyAll(TodoTask.Decide(
            task, new SetTaskDescription(Guid.NewGuid(), user, task.Id, markdown), DateTimeOffset.UtcNow));

        await context.Collection<TodoTask>().InsertOneAsync(task, cancellationToken: ct);
        var loaded = await new MongoDocumentStore<TodoTask>(context).LoadAsync(task.Id, ct);

        Assert.Equal(markdown, loaded!.Description);
    }

    [Fact]
    public async Task ATaskStoredWithoutADescriptionLoadsWithAnEmptyOne()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        var user = Guid.NewGuid();
        var id = Guid.NewGuid();
        var context = TestContext.For(fixture);

        await context.Collection<BsonDocument>("todotasks").InsertOneAsync(new BsonDocument
        {
            ["_id"] = new BsonBinaryData(id, GuidRepresentation.Standard),
            ["userId"] = new BsonBinaryData(user, GuidRepresentation.Standard),
            ["listId"] = new BsonBinaryData(Guid.NewGuid(), GuidRepresentation.Standard),
            ["name"] = "Read a book",
            ["position"] = 0,
            ["version"] = 1,
            ["deleted"] = false,
            ["seq"] = 1L
        }, cancellationToken: ct);

        var loaded = await new MongoDocumentStore<TodoTask>(context).LoadAsync(id, ct);

        Assert.Equal("", loaded!.Description);
    }

    [Fact]
    public async Task ReferenceItemsAreIndexedByUserAndList()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        var context = TestContext.For(fixture);

        await MongoIndexes.EnsureAsync(context, ct);

        var indexes = await context.Collection<BsonDocument>("referenceitems").Indexes.List(ct).ToListAsync(ct);
        var keys = indexes.Select(index => index["key"].AsBsonDocument).ToArray();

        Assert.Contains(keys, key => key.Contains("userId") && key.Contains("seq"));
        Assert.Contains(keys, key => key.Contains("userId") && key.Contains("listId"));
    }
}
