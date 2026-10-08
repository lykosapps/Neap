using Neap.Core.Diagnostics;

namespace Neap.Core.Tests;

public class HeadsetModelsTests
{
    [Theory]
    [InlineData("229B", "Stealth Pro II")]
    [InlineData("2289", "Stealth Pro II")]
    [InlineData("225E", "Atlas Air")]
    [InlineData("2260", "Atlas Air")]
    public void HardwareIsKnownAsItsHeadset(string product, string name) =>
        Assert.Equal(name, HeadsetModels.Of(product)?.Name);

    [Fact]
    public void HardwareNeapDoesNotKnowBelongsToNoHeadset() =>
        Assert.Null(HeadsetModels.Of(0x1234));

    [Fact]
    public void TheStealthProIIsSettingsAreChangedAndAnUnknownHeadsetsAreNot()
    {
        Assert.True(HeadsetModels.StealthProII.Writable);
        Assert.False(HeadsetModels.Of(0x1234) is { Writable: true });
    }

    [Fact]
    public void TheAtlasAirsTransmitterIsATransmitter() =>
        Assert.Equal(Transmitters.Piece.Transmitter, Transmitters.PieceOf("225E"));

    [Fact]
    public void NoProductIdBelongsToTwoHeadsets() =>
        Assert.Equal(
            HeadsetModels.All.Sum(model => model.Hardware.Count),
            HeadsetModels.All.SelectMany(model => model.Hardware.Keys).Distinct(StringComparer.OrdinalIgnoreCase).Count());

    [Fact]
    public void TheAtlasAirShowsNothingItsSupportPagesSayItLacks()
    {
        Assert.DoesNotContain(Feature.ChatWheel, HeadsetModels.AtlasAir.Features);
        Assert.DoesNotContain(Feature.NoiseControl, HeadsetModels.AtlasAir.Features);
        Assert.DoesNotContain(Feature.ModeButton, HeadsetModels.AtlasAir.Features);
        Assert.DoesNotContain(Feature.LowerDial, HeadsetModels.AtlasAir.Features);
        Assert.False(HeadsetModels.AtlasAir.CrossPlay);
    }

    [Fact]
    public void WhatItHasButNeapCannotReadIsOfferedUnread()
    {
        Assert.True(HeadsetModels.Unread(HeadsetModels.AtlasAir, "superhuman_hearing"));
        Assert.True(HeadsetModels.Unread(HeadsetModels.AtlasAir, "noise_gate_threshold"));
        Assert.False(HeadsetModels.Unread(HeadsetModels.AtlasAir, "eq_band_1"));
        Assert.False(HeadsetModels.Unread(HeadsetModels.StealthProII, "superhuman_hearing"));
        Assert.False(HeadsetModels.Unread(null, "superhuman_hearing"));
    }

    [Fact]
    public void EveryUnreadFunctionIsOneTheHeadsetHas() =>
        Assert.All(HeadsetModels.All, model => Assert.Subset(model.Features.ToHashSet(), model.Unread.ToHashSet()));

    [Fact]
    public void TheAtlasAirListsItsOwnButtons() =>
        Assert.Equal(
            [FixedControl.VolumeWheel, FixedControl.FlipToMute, FixedControl.QuickSwitch, FixedControl.BluetoothCallButton],
            HeadsetModels.AtlasAir.Controls);

    [Fact]
    public void BeforeAHeadsetIsKnownTheStealthProIIsButtonsAreListed() =>
        Assert.Equal(HeadsetModels.StealthProII.Controls, HeadsetModels.ControlsOf(null));

    [Theory]
    [InlineData("superhuman_hearing", true)]
    [InlineData("noise_gate_threshold", true)]
    [InlineData("mode_button_function", false)]
    [InlineData("dial_function", false)]
    [InlineData("wake_on_motion", false)]
    [InlineData("eq_band_1", true)]
    [InlineData("mic_monitoring", true)]
    [InlineData("firmware_version", true)]
    public void AnAtlasAirShowsOnlyTheSettingsOfItsFunctions(string setting, bool shown) =>
        Assert.Equal(shown, HeadsetModels.Shows(HeadsetModels.AtlasAir, setting));

    [Fact]
    public void BeforeAHeadsetIsKnownEverythingIsShown() =>
        Assert.True(HeadsetModels.Shows(null, "superhuman_hearing"));

    [Theory]
    [InlineData("Speakers (Stealth Pro II Xbox Headset)", true)]
    [InlineData("Speakers (Atlas Air)", true)]
    [InlineData("Microphone (Atlas Air)", true)]
    [InlineData("Speakers (Realtek(R) Audio)", false)]
    [InlineData("Headset Chat (Turtle Beach) (Virtual Audio Device)", false)]
    public void EveryKnownHeadsetsSoundIsFoundByName(string name, bool headset) =>
        Assert.Equal(headset, HeadsetModels.SoundMatches(name));

    [Fact]
    public void TheSoundNamesLookedForCoverEveryKnownHeadset() =>
        Assert.All(HeadsetModels.All, model =>
            Assert.True(HeadsetModels.SoundMatches(model.SoundName)));
}
