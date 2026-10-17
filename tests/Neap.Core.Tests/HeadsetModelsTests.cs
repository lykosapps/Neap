using System.Text.Json;
using Neap.Core.Diagnostics;
using Neap.Core.Protocol;
using Neap.Core.Settings;

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
    [InlineData("mic_monitoring", false)]
    [InlineData("firmware_version", true)]
    public void AnAtlasAirShowsOnlyTheSettingsOfItsFunctions(string setting, bool shown) =>
        Assert.Equal(shown, HeadsetModels.Shows(HeadsetModels.AtlasAir, setting));

    [Theory]
    [InlineData("Stealth Pro II", 0x2A0)]
    [InlineData("Atlas Air", 0x260)]
    public void EachHeadsetKeepsItsCopyOfTheSystemVolumeUnderItsOwnKey(string name, int key) =>
        Assert.Equal(key, HeadsetModels.VolumeKeyOf(HeadsetModels.All.Single(model => model.Name == name)));

    [Fact]
    public void BeforeAHeadsetIsKnownItsVolumeIsTheStealthProIIs() =>
        Assert.Equal(0x2A0, HeadsetModels.VolumeKeyOf(null));

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

    [Fact]
    public void TheAtlasAirIsWrittenToItsOwnKeysAndTheStealthProIIToTheStandardOnes()
    {
        Assert.Equal(0x820, HeadsetModels.KeyOnHeadset(HeadsetModels.AtlasAir, 0x740));
        Assert.Equal(0xC00, HeadsetModels.KeyOnHeadset(HeadsetModels.AtlasAir, 0x2D0));
        Assert.Equal(0x740, HeadsetModels.KeyOnHeadset(HeadsetModels.StealthProII, 0x740));
        Assert.Equal(0x740, HeadsetModels.KeyOnHeadset(null, 0x740));
    }

    [Fact]
    public void TheAtlasAirsReadingsMoveToTheStandardKeysAndItsStandardOnesAreDropped()
    {
        var reported = new Dictionary<string, JsonElement>
        {
            ["800"] = JsonSerializer.SerializeToElement(1),
            ["820"] = JsonSerializer.SerializeToElement(60),
            ["730"] = JsonSerializer.SerializeToElement(2),
            ["240"] = JsonSerializer.SerializeToElement(34),
        };

        var standard = HeadsetModels.AtStandardKeys(HeadsetModels.AtlasAir, reported);

        Assert.Equal(1, standard["720"].GetInt32());
        Assert.Equal(60, standard["740"].GetInt32());
        Assert.False(standard.ContainsKey("730"));
        Assert.Equal(34, standard["240"].GetInt32());
    }

    [Fact]
    public void ReadingsFromOtherHeadsetsAreLeftAlone()
    {
        var reported = new Dictionary<string, JsonElement> { ["740"] = JsonSerializer.SerializeToElement(9) };

        Assert.Same(reported, HeadsetModels.AtStandardKeys(HeadsetModels.StealthProII, reported));
        Assert.Same(reported, HeadsetModels.AtStandardKeys(null, reported));
    }

    [Fact]
    public void EveryKeyTheAtlasAirKeepsElsewhereIsInTheRegistryAndItsGroupCanBeAsked()
    {
        Assert.All(HeadsetModels.AtlasAir.OwnKeys.Values, key => Assert.True(Registry.ByKey[key].Writable));
        Assert.All(HeadsetModels.AtlasAir.ExtraCategories, group => Assert.True(Verbs.Readers.ContainsKey(group)));
    }

    public static IEnumerable<object[]> ModelNames =>
        HeadsetModels.All.Select(model => new object[] { model.Name });

    /// <summary>
    /// Every model is fully wired: the settings its functions read, its
    /// relocated keys and its volume key are all in the registry, and its extra
    /// groups can be asked. This holds a new headset to the same wiring as the
    /// two here, so a gap shows as a failing test rather than a blank control.
    /// </summary>
    [Theory]
    [MemberData(nameof(ModelNames))]
    public void EveryModelIsFullyWired(string name)
    {
        var model = HeadsetModels.All.Single(m => m.Name == name);

        foreach (var feature in model.Features)
            foreach (string setting in HeadsetCheck.Needs[feature])
                Assert.True(Registry.ByName.ContainsKey(setting),
                    $"{name}: {feature} reads '{setting}', which the registry does not have");

        foreach (int key in model.OwnKeys.Values)
            Assert.True(Registry.ByKey.ContainsKey(key), $"{name}: relocated key 0x{key:x} is not in the registry");

        Assert.True(Registry.ByKey.ContainsKey(model.VolumeKey),
            $"{name}: volume key 0x{model.VolumeKey:x} is not in the registry");

        foreach (string category in model.ExtraCategories)
            Assert.True(Verbs.Readers.ContainsKey(category), $"{name}: extra group '{category}' has no read verb");

        Assert.Subset(model.Features.ToHashSet(), model.Unread.ToHashSet());
    }
}
