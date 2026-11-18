using PSPad.TestInfrastructure;

namespace PSPad.Module.Reminders.Tests;

[UnitTest]
public class PushAnswerTests
{
    [Theory]
    [InlineData(201, PushOutcome.Delivered)]
    [InlineData(200, PushOutcome.Delivered)]
    [InlineData(404, PushOutcome.Gone)]
    [InlineData(410, PushOutcome.Gone)]
    [InlineData(429, PushOutcome.Retry)]
    [InlineData(500, PushOutcome.Retry)]
    [InlineData(503, PushOutcome.Retry)]
    [InlineData(null, PushOutcome.Retry)]
    [InlineData(400, PushOutcome.Failed)]
    [InlineData(403, PushOutcome.Failed)]
    [InlineData(413, PushOutcome.Failed)]
    public void Maps_status_to_outcome(int? status, PushOutcome expected) =>
        Assert.Equal(expected, PushAnswer.Of(status));
}
