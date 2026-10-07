using Neap.Core.Input;

namespace Neap.Core.Tests.Input;

public class DesktopEntryTests
{
    [Fact]
    public void AnOrdinaryPathIsQuotedAndFollowedByItsArguments() =>
        Assert.Equal("\"/home/deck/Neap-test/Neap.Desktop\" --startup",
            DesktopEntry.Exec("/home/deck/Neap-test/Neap.Desktop", "--startup"));

    [Fact]
    public void ASpaceNeedsNoEscape() =>
        Assert.Equal("\"/home/sam/My Apps/Neap\" --startup", DesktopEntry.Exec("/home/sam/My Apps/Neap", "--startup"));

    [Theory]
    [InlineData("/home/a$b/Neap", "\"/home/a\\\\$b/Neap\"")]        // $ is escaped, and the escape's backslash is doubled
    [InlineData("/home/a\"b/Neap", "\"/home/a\\\\\"b/Neap\"")]      // a double quote
    [InlineData("/home/a`b/Neap", "\"/home/a\\\\`b/Neap\"")]        // a backtick
    [InlineData("/home/a\\b/Neap", "\"/home/a\\\\\\\\b/Neap\"")]    // a literal backslash takes four
    [InlineData("/home/100%/Neap", "\"/home/100%%/Neap\"")]          // a percent sign is a field code unless doubled
    public void AReservedCharacterIsEscapedAsTheSpecificationSays(string path, string written) =>
        Assert.Equal(written, DesktopEntry.Exec(path));

    [Fact]
    public void APathWithALineBreakIsRefused() =>
        Assert.Throws<FormatException>(() => DesktopEntry.Exec("/home/a\nb/Neap"));

    [Fact]
    public void APlainValueDoublesItsBackslashesAndRefusesALineBreak()
    {
        Assert.Equal("/home/a\\\\b", DesktopEntry.Value("/home/a\\b"));
        Assert.Throws<FormatException>(() => DesktopEntry.Value("a\r\nb"));
    }
}
