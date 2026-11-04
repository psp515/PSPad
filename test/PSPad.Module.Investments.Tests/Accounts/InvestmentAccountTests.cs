using PSPad.Abstractions;
using PSPad.Module.Investments.Accounts;
using PSPad.TestInfrastructure;
using static PSPad.Module.Investments.Tests.Given;

namespace PSPad.Module.Investments.Tests.Accounts;

[UnitTest]
public class InvestmentAccountTests
{
    static CreateInvestmentAccount ACreate(string name = "IKE", string currency = "PLN", string? number = null) =>
        new(Guid.NewGuid(), Owner, AccountId, name, currency, Broker.Xtb, number);

    static string Rejection(InvestmentAccount? account, ICommand command) =>
        Assert.Throws<DomainRejectedException>(() => InvestmentAccount.Decide(account, command, Now)).Message;

    static InvestmentAccount Applied(InvestmentAccount account, ICommand command)
    {
        account.ApplyAll(InvestmentAccount.Decide(account, command, Now));
        return account;
    }

    [Fact]
    public void CreatingKeepsTheTrimmedValues()
    {
        var account = new InvestmentAccount();

        account.ApplyAll(InvestmentAccount.Decide(null, ACreate("  IKE XTB ", " usd ", " 12345 "), Now));

        Assert.Equal(AccountId, account.Id);
        Assert.Equal(Owner, account.UserId);
        Assert.Equal("IKE XTB", account.Name);
        Assert.Equal("USD", account.Currency);
        Assert.Equal(Broker.Xtb, account.Broker);
        Assert.Equal("12345", account.BrokerAccountNumber);
        Assert.Equal(0, account.OperationCount);
        Assert.Equal(Now, account.CreatedAt);
        Assert.False(account.IsArchived);
    }

    [Fact]
    public void ABlankNumberIsNoNumber()
    {
        var account = new InvestmentAccount();

        account.ApplyAll(InvestmentAccount.Decide(null, ACreate(number: "   "), Now));

        Assert.Null(account.BrokerAccountNumber);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ABlankNameIsRejected(string name) =>
        Assert.Equal("An account needs a name.", Rejection(null, ACreate(name)));

    [Fact]
    public void ANameOver64CharactersIsRejected() =>
        Assert.Equal("An account name is at most 64 characters.", Rejection(null, ACreate(new string('a', 65))));

    [Fact]
    public void ANameOfExactly64CharactersIsAllowed() =>
        Assert.Single(InvestmentAccount.Decide(null, ACreate(new string('a', 64)), Now));

    [Fact]
    public void AnUnknownCurrencyIsRejected() =>
        Assert.Throws<DomainRejectedException>(() => InvestmentAccount.Decide(null, ACreate(currency: "XYZ"), Now));

    [Fact]
    public void ANumberOver32CharactersIsRejected() =>
        Assert.Throws<DomainRejectedException>(() => InvestmentAccount.Decide(null, ACreate(number: new string('1', 33)), Now));

    [Fact]
    public void CreatingAnExistingIdIsRejected() =>
        Assert.Equal("That account already exists.", Rejection(AnAccount(id: AccountId), ACreate()));

    [Fact]
    public void RenamingChangesTheName() =>
        Assert.Equal("Main", Applied(AnAccount(), new RenameInvestmentAccount(Guid.NewGuid(), Owner, Guid.Empty, " Main ")).Name);

    [Fact]
    public void RenamingToTheSameNameChangesNothing()
    {
        var account = AnAccount();

        Assert.Empty(InvestmentAccount.Decide(account, new RenameInvestmentAccount(Guid.NewGuid(), Owner, account.Id, "IKE"), Now));
    }

    [Fact]
    public void ArchivingTwiceChangesNothing()
    {
        var account = Archive(AnAccount());

        Assert.True(account.IsArchived);
        Assert.Equal(Now, account.ArchivedAt);
        Assert.Empty(InvestmentAccount.Decide(account, new ArchiveInvestmentAccount(Guid.NewGuid(), Owner, account.Id), Now));
    }

    [Fact]
    public void RestoringBringsAnArchivedAccountBack()
    {
        var account = Applied(Archive(AnAccount()), new RestoreInvestmentAccount(Guid.NewGuid(), Owner, Guid.Empty));

        Assert.False(account.IsArchived);
    }

    [Fact]
    public void RestoringALiveAccountChangesNothing()
    {
        var account = AnAccount();

        Assert.Empty(InvestmentAccount.Decide(account, new RestoreInvestmentAccount(Guid.NewGuid(), Owner, account.Id), Now));
    }

    [Fact]
    public void AnArchivedAccountRejectsARename() =>
        Assert.Equal("This account is archived.",
            Rejection(Archive(AnAccount()), new RenameInvestmentAccount(Guid.NewGuid(), Owner, Guid.Empty, "Main")));

    [Fact]
    public void AnArchivedAccountRejectsANumber() =>
        Assert.Equal("This account is archived.",
            Rejection(Archive(AnAccount()), new SetBrokerAccountNumber(Guid.NewGuid(), Owner, Guid.Empty, "123")));

    [Fact]
    public void AnEmptyNumberIsSet() =>
        Assert.Equal("51234",
            Applied(AnAccount(), new SetBrokerAccountNumber(Guid.NewGuid(), Owner, Guid.Empty, " 51234 ")).BrokerAccountNumber);

    [Fact]
    public void SettingTheSameNumberChangesNothing()
    {
        var account = AnAccount(number: "51234");

        Assert.Empty(InvestmentAccount.Decide(account, new SetBrokerAccountNumber(Guid.NewGuid(), Owner, account.Id, "51234"), Now));
    }

    [Fact]
    public void ADifferentNumberIsRejected() =>
        Assert.Equal("This account already has broker account number 51234.",
            Rejection(AnAccount(number: "51234"), new SetBrokerAccountNumber(Guid.NewGuid(), Owner, Guid.Empty, "99999")));

    [Fact]
    public void AStrangerCannotTouchTheAccount()
    {
        var rejection = Assert.Throws<DomainRejectedException>(() => InvestmentAccount.Decide(AnAccount(),
            new RenameInvestmentAccount(Guid.NewGuid(), Stranger, Guid.Empty, "Mine"), Now));

        Assert.Equal("That account does not exist.", rejection.Message);
        Assert.True(rejection.Unrecoverable);
    }

    [Fact]
    public void AMissingAccountIsUnrecoverable()
    {
        var rejection = Assert.Throws<DomainRejectedException>(() =>
            InvestmentAccount.Decide(null, new ArchiveInvestmentAccount(Guid.NewGuid(), Owner, AccountId), Now));

        Assert.Equal("That account does not exist.", rejection.Message);
        Assert.True(rejection.Unrecoverable);
    }
}
