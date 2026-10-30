namespace Neap.Core.Tests;

public class AppFolderTests
{
    [Theory]
    [InlineData("Pretend")]
    [InlineData("Pretend-2")]
    [InlineData("run 3")]
    public void APlainNameIsAFolderOfItsOwn(string name) => Assert.True(AppFolder.IsFolderName(name));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData(".")]
    [InlineData("..")]
    [InlineData("a/b")]
    [InlineData("a\\b")]
    [InlineData("../Neap")]
    [InlineData("/tmp/neap")]
    [InlineData("C:\\Neap")]
    [InlineData("C:Neap")]
    public void ANameThatLeadsAnywhereElseIsNot(string? name) => Assert.False(AppFolder.IsFolderName(name));
}
