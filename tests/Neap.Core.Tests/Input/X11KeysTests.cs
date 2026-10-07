using Neap.Core.Input;

namespace Neap.Core.Tests.Input;

public class X11KeysTests
{
    [Theory]
    [InlineData(0x41u, 0x61u)]   // A
    [InlineData(0x5Au, 0x7Au)]   // Z
    [InlineData(0x30u, 0x30u)]   // 0
    [InlineData(0x39u, 0x39u)]   // 9
    [InlineData(0x70u, 0xFFBEu)] // F1
    [InlineData(0x87u, 0xFFD5u)] // F24
    [InlineData(0x21u, 0xFF55u)] // Page Up
    [InlineData(0x22u, 0xFF56u)] // Page Down
    [InlineData(0x23u, 0xFF57u)] // End
    [InlineData(0x24u, 0xFF50u)] // Home
    [InlineData(0x25u, 0xFF51u)] // Left
    [InlineData(0x28u, 0xFF54u)] // Down
    [InlineData(0x2Du, 0xFF63u)] // Insert
    [InlineData(0x2Eu, 0xFFFFu)] // Delete
    [InlineData(0x20u, 0x20u)]   // Space
    [InlineData(0xBCu, 0x2Cu)]   // ,
    [InlineData(0xBFu, 0x2Fu)]   // /
    [InlineData(0xDDu, 0x5Du)]   // ]
    public void AKeyIsNamedAsXNamesIt(uint virtualKey, uint keysym) =>
        Assert.Equal(keysym, X11Keys.KeySym(virtualKey));

    [Theory]
    [InlineData(0x00u)]
    [InlineData(0x0Du)] // Enter: not offered
    [InlineData(0x1Bu)] // Escape: not offered
    [InlineData(0x88u)]
    [InlineData(0xFFu)]
    public void AKeyNoShortcutOffersHasNoName(uint virtualKey) =>
        Assert.Null(X11Keys.KeySym(virtualKey));

    [Fact]
    public void EveryKeyTheShortcutScreenOffersHasAName()
    {
        // The keys Shortcut can name and the screen's capture can record.
        var offered = new List<uint>();
        offered.AddRange(Enumerable.Range(0x41, 26).Select(k => (uint)k));
        offered.AddRange(Enumerable.Range(0x30, 10).Select(k => (uint)k));
        offered.AddRange(Enumerable.Range(0x70, 24).Select(k => (uint)k));
        offered.AddRange([0x21u, 0x22, 0x23, 0x24, 0x25, 0x26, 0x27, 0x28, 0x2D, 0x2E, 0x20, 0xBC, 0xBE, 0xBF, 0xDB, 0xDD]);

        Assert.All(offered, key => Assert.NotNull(X11Keys.KeySym(key)));
        Assert.Equal(offered.Count, offered.Select(key => X11Keys.KeySym(key)!.Value).Distinct().Count());
    }

    [Theory]
    [InlineData(true, false, false, false, 0x04u)]
    [InlineData(false, true, false, false, 0x08u)]
    [InlineData(false, false, true, false, 0x01u)]
    [InlineData(false, false, false, true, 0x40u)]
    [InlineData(true, true, false, false, 0x0Cu)]
    [InlineData(false, false, false, false, 0x00u)]
    public void ModifiersAreTheBitsAPressCarries(bool control, bool alt, bool shift, bool windows, uint bits) =>
        Assert.Equal(bits, X11Keys.Modifiers(control, alt, shift, windows));

    [Fact]
    public void ALockBeingOnIsNotPartOfTheShortcut()
    {
        uint controlAltWithBothLocksOn = X11Keys.Control | X11Keys.Alt | X11Keys.CapsLock | X11Keys.NumLock;

        Assert.Equal(X11Keys.Control | X11Keys.Alt, X11Keys.WithoutLocks(controlAltWithBothLocksOn));
    }

    [Fact]
    public void AMouseButtonHeldIsNotPartOfTheShortcut() =>
        Assert.Equal(X11Keys.Shift, X11Keys.WithoutLocks(X11Keys.Shift | 0x100 /* Button1Mask */));
}
