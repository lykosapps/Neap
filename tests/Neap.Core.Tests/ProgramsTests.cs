namespace Neap.Core.Tests;

public sealed class ProgramsTests
{
    [Theory]
    [InlineData("ms-teams", "Ms Teams")]
    [InlineData("discord", "Discord")]
    [InlineData("Discord", "Discord")]
    [InlineData("zoom_us", "Zoom Us")]
    [InlineData("witcher3", "Witcher3")]
    [InlineData("--", "")]
    [InlineData("", "")]
    public void ReadableTurnsAProcessNameIntoWords(string name, string expected) =>
        Assert.Equal(expected, Programs.Readable(name));
}
