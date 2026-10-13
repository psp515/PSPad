using System.Text.Json.Serialization;
using PSPad.Abstractions;

namespace PSPad.Module.Money.Budgets;

public sealed class Budget : Aggregate
{
    public const int MaxNameLength = ContentLimits.Name;

    public const int MaxCategoriesPerKind = 64;

    public static IReadOnlyList<string> SeedExpenseCategories { get; } =
        ["Home", "Food", "Eating Out", "PC", "Bike", "Car", "Gifts", "Clothes"];

    public static IReadOnlyList<string> SeedIncomeCategories { get; } =
        ["Salary", "Freelance", "Interest", "Gifts", "Other"];

    [JsonInclude]
    public string Name { get; private set; } = "";

    [JsonInclude]
    public DateTimeOffset CreatedAt { get; private set; }

    [JsonInclude]
    public DateTimeOffset? ArchivedAt { get; private set; }

    [JsonInclude]
    public string? InviteToken { get; private set; }

    [JsonInclude]
    public string? InviteCode { get; private set; }

    [JsonInclude]
    public DateTimeOffset? InviteExpiresAt { get; private set; }

    [JsonInclude]
    public int WrongCodes { get; private set; }

    [JsonInclude]
    public string? OwnerName { get; private set; }

    [JsonInclude]
    List<BudgetMember> _members = [];

    [JsonInclude]
    List<string> _expenseCategories = [];

    [JsonInclude]
    List<string> _incomeCategories = [];

    public IReadOnlyList<string> ExpenseCategories => _expenseCategories;

    public IReadOnlyList<string> IncomeCategories => _incomeCategories;

    public bool IsArchived => ArchivedAt is not null;

    public IReadOnlyList<BudgetMember> Members => _members;

    public bool IsShared => _members.Count > 0 || InviteToken is not null;

    public bool ClosedByWrongCodes => InviteToken is null && WrongCodes >= Invites.MostWrongCodes;

    public bool HasMember(Guid userId) => _members.Any(member => member.UserId == userId);

    public bool Admits(Guid userId) => UserId == userId || HasMember(userId);

    public bool IsInviteLiveAt(DateTimeOffset now) => Invites.IsLiveAt(InviteToken, InviteCode, InviteExpiresAt, now);

    public InviteCheck CheckInvite(string token, string code, DateTimeOffset now) =>
        Invites.Check(InviteToken, InviteCode, InviteExpiresAt, token, code, now);

    public IReadOnlyList<string> CategoriesOf(CategoryKind kind) =>
        kind == CategoryKind.Expense ? _expenseCategories : _incomeCategories;

    public static IReadOnlyList<DomainEvent> Decide(Budget? budget, ICommand command, DateTimeOffset at)
    {
        switch (command)
        {
            case CreateBudget create:
                if (budget is not null)
                {
                    throw new DomainRejectedException("That budget already exists.");
                }

                return [new BudgetCreated(create.BudgetId, create.UserId, at, RequireName(create.Name),
                    SeedExpenseCategories, SeedIncomeCategories)];

            case RenameBudget rename:
                var renaming = BudgetAccess.Writable(BudgetAccess.Owned(budget, rename.UserId));
                var name = RequireName(rename.Name);
                return renaming.Name == name ? [] : [new BudgetRenamed(renaming.Id, renaming.UserId, at, name)];

            case ArchiveBudget archive:
                var archiving = BudgetAccess.Owned(budget, archive.UserId);
                return archiving.IsArchived ? [] : [new BudgetArchived(archiving.Id, archiving.UserId, at)];

            case RestoreBudget restore:
                var restoring = BudgetAccess.Owned(budget, restore.UserId);
                return restoring.IsArchived ? [new BudgetRestored(restoring.Id, restoring.UserId, at)] : [];

            case AddCategory add:
                var adding = BudgetAccess.Writable(BudgetAccess.To(budget, add.UserId));
                var category = CategoryName.Normalize(add.Name);
                var existing = adding.CategoriesOf(add.Kind);
                if (CategoryName.IndexIn(existing, category) >= 0)
                {
                    return [];
                }

                return existing.Count >= MaxCategoriesPerKind
                    ? throw new DomainRejectedException(
                        $"A budget can have at most {MaxCategoriesPerKind} {add.Kind.ToString().ToLowerInvariant()} categories.")
                    : [new CategoryAdded(adding.Id, adding.UserId, at, add.Kind, category)];

            case RenameCategory renameCategory:
                var renamingCategory = BudgetAccess.Writable(BudgetAccess.To(budget, renameCategory.UserId));
                var renameList = renamingCategory.CategoriesOf(renameCategory.Kind);
                var source = RequireCategory(renameList, renameCategory.From);
                var target = CategoryName.Normalize(renameCategory.To);
                var clash = CategoryName.IndexIn(renameList, target);
                if (clash >= 0 && clash != source)
                {
                    throw new DomainRejectedException("That category already exists. Merge them instead.");
                }

                return renameList[source] == target
                    ? []
                    : [new CategoryRenamed(renamingCategory.Id, renamingCategory.UserId, at, renameCategory.Kind,
                        renameList[source], target)];

            case MergeCategory mergeCategory:
                var merging = BudgetAccess.Writable(BudgetAccess.To(budget, mergeCategory.UserId));
                var mergeList = merging.CategoriesOf(mergeCategory.Kind);
                var mergeFrom = RequireCategory(mergeList, mergeCategory.From);
                var mergeInto = RequireCategory(mergeList, mergeCategory.Into);
                return mergeFrom == mergeInto
                    ? throw new DomainRejectedException("A category cannot be merged into itself.")
                    : [new CategoriesMerged(merging.Id, merging.UserId, at, mergeCategory.Kind, mergeList[mergeFrom], mergeList[mergeInto])];

            case RemoveCategory removeCategory:
                var removing = BudgetAccess.Writable(BudgetAccess.To(budget, removeCategory.UserId));
                var removeList = removing.CategoriesOf(removeCategory.Kind);
                return [new CategoryRemoved(removing.Id, removing.UserId, at, removeCategory.Kind,
                    removeList[RequireCategory(removeList, removeCategory.Name)])];

            case ShareBudget share:
                var sharing = BudgetAccess.Writable(BudgetAccess.Owned(budget, share.UserId));
                var token = Invites.RequireToken(share.Token);
                var code = Invites.RequireCode(share.Code);
                var ownerName = ContentLimits.Cut(share.OwnerName.Trim(), ContentLimits.Name);
                return sharing.InviteToken == token && sharing.InviteCode == code && sharing.OwnerName == ownerName
                    && sharing.IsInviteLiveAt(at)
                    ? []
                    : [new BudgetShared(sharing.Id, sharing.UserId, at, token, code, ownerName)];

            case StopSharingBudget stop:
                var stopping = BudgetAccess.Owned(budget, stop.UserId);
                return stopping.InviteToken is null ? [] : [new BudgetSharingStopped(stopping.Id, stopping.UserId, at)];

            case RemoveBudgetMember remove:
                var removingMember = BudgetAccess.Owned(budget, remove.UserId);
                return removingMember.HasMember(remove.MemberId)
                    ? [new BudgetMemberRemoved(removingMember.Id, removingMember.UserId, at, remove.MemberId)]
                    : throw new DomainRejectedException("That person is not a member of this budget.");

            case LeaveBudget leave:
                var leaving = budget ?? throw new DomainRejectedException("That budget does not exist.");
                return leaving.HasMember(leave.UserId)
                    ? [new BudgetLeft(leaving.Id, leaving.UserId, at, leave.UserId)]
                    : throw new DomainRejectedException(leaving.UserId == leave.UserId
                        ? "The owner cannot leave their own budget."
                        : "You are not a member of this budget.");

            case JoinBudget join:
                var joining = budget ?? throw new DomainRejectedException("This invite link no longer works.");
                if (joining.UserId == join.UserId || joining.HasMember(join.UserId))
                {
                    return [];
                }

                return joining.CheckInvite(join.Token, join.Code, at) switch
                {
                    InviteCheck.Open when joining._members.Count >= ContentLimits.MembersPerBudget =>
                        throw new DomainRejectedException("This budget already has as many members as it can hold."),
                    InviteCheck.Open =>
                        [new BudgetJoined(joining.Id, joining.UserId, at, join.UserId, RequireMemberName(join.DisplayName))],
                    InviteCheck.WrongCode =>
                        [new BudgetInviteCodeRejected(joining.Id, joining.UserId, at, joining.WrongCodes + 1)],
                    InviteCheck.Expired => throw new DomainRejectedException("This invite has expired."),
                    _ => throw new DomainRejectedException("This invite link no longer works.")
                };

            default:
                throw new DomainRejectedException($"A budget cannot handle {command.GetType().Name}.");
        }
    }

    protected override void When(DomainEvent @event)
    {
        switch (@event)
        {
            case BudgetCreated created:
                Id = created.AggregateId;
                UserId = created.UserId;
                Name = created.Name;
                CreatedAt = created.At;
                _expenseCategories = [.. created.ExpenseCategories];
                _incomeCategories = [.. created.IncomeCategories];
                break;
            case BudgetRenamed renamed:
                Name = renamed.Name;
                break;
            case BudgetArchived archived:
                ArchivedAt = archived.At;
                CloseInvite();
                break;
            case BudgetRestored:
                ArchivedAt = null;
                break;
            case BudgetShared shared:
                InviteToken = shared.Token;
                InviteCode = shared.Code;
                InviteExpiresAt = shared.At + Invites.Life;
                WrongCodes = 0;
                OwnerName = shared.OwnerName;
                break;
            case BudgetSharingStopped:
                CloseInvite();
                break;
            case BudgetInviteCodeRejected rejected:
                WrongCodes = rejected.WrongCodes;
                if (WrongCodes >= Invites.MostWrongCodes)
                {
                    CloseInvite();
                }

                break;
            case BudgetJoined joined:
                _members.Add(new BudgetMember(joined.MemberId, joined.DisplayName, joined.At));
                break;
            case BudgetMemberRemoved removed:
                _members.RemoveAll(member => member.UserId == removed.MemberId);
                break;
            case BudgetLeft left:
                _members.RemoveAll(member => member.UserId == left.MemberId);
                break;
            case CategoryAdded added:
                ListOf(added.Kind).Add(added.Name);
                break;
            case CategoryRenamed renamed:
                var renamedList = ListOf(renamed.Kind);
                renamedList[CategoryName.IndexIn(renamedList, renamed.From)] = renamed.To;
                break;
            case CategoriesMerged merged:
                var mergedList = ListOf(merged.Kind);
                mergedList.RemoveAt(CategoryName.IndexIn(mergedList, merged.From));
                break;
            case CategoryRemoved removed:
                var removedList = ListOf(removed.Kind);
                removedList.RemoveAt(CategoryName.IndexIn(removedList, removed.Name));
                break;
        }
    }

    void CloseInvite()
    {
        InviteToken = null;
        InviteCode = null;
        InviteExpiresAt = null;
    }

    List<string> ListOf(CategoryKind kind) => kind == CategoryKind.Expense ? _expenseCategories : _incomeCategories;

    static int RequireCategory(IReadOnlyList<string> names, string name)
    {
        var index = CategoryName.IndexIn(names, name);
        return index < 0 ? throw new DomainRejectedException("That category does not exist.") : index;
    }

    static string RequireName(string name)
    {
        var trimmed = name.Trim();

        if (trimmed.Length == 0)
        {
            throw new DomainRejectedException("A budget needs a name.");
        }

        return trimmed.Length > MaxNameLength
            ? throw new DomainRejectedException($"A budget name is at most {MaxNameLength} characters.")
            : trimmed;
    }

    static string RequireMemberName(string name) =>
        string.IsNullOrWhiteSpace(name)
            ? throw new DomainRejectedException("A member needs a name.")
            : ContentLimits.Cut(name.Trim(), ContentLimits.Name);
}
