using Neap.Core.Settings;

namespace Neap.Core.Tests.Settings;

public class HeadsetLabelTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ABlankNameIsNoName(string? reported) => Assert.Null(HeadsetLabel.Given(reported));

    [Fact]
    public void AGivenNameIsShownWithoutItsPadding() => Assert.Equal("Desk", HeadsetLabel.Given(" Desk "));
}
