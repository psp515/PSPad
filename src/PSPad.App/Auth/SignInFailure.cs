namespace PSPad.App.Auth;

public sealed class SignInFailure
{
    public bool Happened { get; private set; }

    public void Record() => Happened = true;

    public void Clear() => Happened = false;
}
