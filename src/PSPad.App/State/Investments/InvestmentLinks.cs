namespace PSPad.App.State.Investments;

public static class InvestmentLinks
{
    public const string Index = "/investments";

    const string New = "new";
    const string OperationKey = "investmentoperation";
    const string AccountKey = "investmentaccount";
    const string ImportAccountKey = "account";

    public static string For(Guid? accountId, int tab = InvestmentTabs.Positions)
    {
        var root = accountId is { } id ? $"{Index}/{id}" : Index;
        return tab == InvestmentTabs.Positions ? root : $"{root}/{InvestmentTabs.NameOf(tab)}";
    }

    public static string ForImport(Guid? accountId) =>
        accountId is { } id ? $"{Index}/import?{ImportAccountKey}={id}" : $"{Index}/import";

    public static string ForOperation(string uri, Guid operationId) => $"{QueryString.Without(uri)}?{OperationKey}={operationId}";

    public static string ForNewOperation(string uri) => $"{QueryString.Without(uri)}?{OperationKey}={New}";

    public static string ForAccount(string uri, Guid accountId) => $"{QueryString.Without(uri)}?{AccountKey}={accountId}";

    public static string ForNewAccount(string uri) => $"{QueryString.Without(uri)}?{AccountKey}={New}";

    public static Guid? OperationFrom(string uri) => IdFrom(uri, OperationKey);

    public static bool IsNewOperation(string uri) => QueryString.Value(uri, OperationKey) == New;

    public static Guid? AccountFrom(string uri) => IdFrom(uri, AccountKey);

    public static bool IsNewAccount(string uri) => QueryString.Value(uri, AccountKey) == New;

    public static Guid? AccountInPath(string uri)
    {
        var path = new Uri(QueryString.Without(uri), UriKind.Absolute).AbsolutePath.Trim('/').Split('/');
        return path.Length >= 2 && path[0] == Index.Trim('/') && Guid.TryParse(path[1], out var id) ? id : null;
    }

    static Guid? IdFrom(string uri, string key) => Guid.TryParse(QueryString.Value(uri, key), out var id) ? id : null;
}
