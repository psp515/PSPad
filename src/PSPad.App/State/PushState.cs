using PSPad.App.Push;

namespace PSPad.App.State;

public static class PushState
{
    public static PushCardState Of(PushSupport support) => support switch
    {
        { NeedsHomeScreen: true } => PushCardState.NeedsHomeScreen,
        { Supported: false } => PushCardState.Unsupported,
        { Permission: "denied" } => PushCardState.Denied,
        { Endpoint: not null, Permission: "granted" } => PushCardState.On,
        _ => PushCardState.Off
    };
}
