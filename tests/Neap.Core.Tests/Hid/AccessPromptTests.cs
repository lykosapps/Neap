using Neap.Core.Hid;

namespace Neap.Core.Tests.Hid;

public class AccessPromptTests
{
    [Fact]
    public void TheProgramRunAsRootIsNamedByItsFullPath()
    {
        Assert.StartsWith("/", AccessPrompt.Arguments[0], StringComparison.Ordinal);
        Assert.Equal(["-c", UdevRule.RootCommand], AccessPrompt.Arguments.Skip(1));
    }

    [Theory]
    [InlineData(0, "", AccessResult.Granted)]
    [InlineData(126, "", AccessResult.Cancelled)]
    [InlineData(127, "Error executing command as another user: Not authorized\n\nThis incident has been reported.\n", AccessResult.Cancelled)]
    [InlineData(127, "Error executing command as another user: No authentication agent found.\n", AccessResult.Failed)]
    [InlineData(127, "sh: 1: udevadm: not found\n", AccessResult.Failed)]
    [InlineData(127, "", AccessResult.Failed)]
    [InlineData(1, "sh: cannot create /etc/udev/rules.d/70-neap.rules: Read-only file system\n", AccessResult.Failed)]
    public void OnlyAPromptTurnedAwayIsCancelled(int exitCode, string said, AccessResult expected) =>
        Assert.Equal(expected, AccessPrompt.Outcome(exitCode, said));
}
