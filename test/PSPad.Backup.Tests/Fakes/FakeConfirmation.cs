using PSPad.Backup.Runs;

namespace PSPad.Backup.Tests.Fakes;

sealed class FakeConfirmation(bool answer) : IConfirmation
{
    public bool Asked { get; private set; }

    public bool Confirm(string word, string backup)
    {
        Asked = true;
        return answer;
    }
}
