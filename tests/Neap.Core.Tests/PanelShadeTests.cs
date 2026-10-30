namespace Neap.Core.Tests;

public class PanelShadeTests
{
    [Theory]
    [InlineData("KDE")]
    [InlineData("XFCE")]
    [InlineData(null)]
    public void ALightThemeMeansALightPanel(string? desktop) =>
        Assert.True(PanelShade.IsLight(desktop, lightTheme: true));

    [Theory]
    [InlineData("KDE")]
    [InlineData("GNOME")]
    [InlineData(null)]
    public void ADarkThemeMeansADarkPanel(string? desktop) =>
        Assert.False(PanelShade.IsLight(desktop, lightTheme: false));

    [Theory]
    [InlineData("GNOME")]
    [InlineData("ubuntu:GNOME")]
    [InlineData("pop:GNOME")]
    public void GnomesTopBarIsDarkWhateverTheTheme(string desktop) =>
        Assert.False(PanelShade.IsLight(desktop, lightTheme: true));
}
