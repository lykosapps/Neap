using Neap.Core.Mix;

namespace Neap.Core.Tests.Mix;

public class HeadsetOutputTests
{
    private static readonly SoundOutput Dock = new("dock", "Stealth Pro 2 Wireless (Stealth Pro II Xbox)");
    private static readonly SoundOutput Usb = new("usb", "Stealth Pro 2 Wireless (USB Transmitter)");

    [Fact]
    public void TheDefaultOutputIsTheHeadsetsWhenItIsOne() =>
        Assert.Equal(("dock", HeadsetFound.Default), HeadsetOutput.Pick([Dock], "dock", "speakers"));

    [Fact]
    public void WithTheSpeakersTheDefaultTheOutputForCallsIsTheHeadsets() =>
        Assert.Equal(("dock", HeadsetFound.Communications), HeadsetOutput.Pick([Dock], "speakers", "dock"));

    [Fact]
    public void WithNeitherDefaultTheOnlyHeadsetOutputIsUsed() =>
        Assert.Equal(("dock", HeadsetFound.OnlyOne), HeadsetOutput.Pick([Dock], "speakers", "speakers"));

    [Fact]
    public void TwoHeadsetOutputsAndNeitherDefaultGiveNone() =>
        Assert.Equal((null, HeadsetFound.Several), HeadsetOutput.Pick([Dock, Usb], "speakers", null));

    [Fact]
    public void OfTwoHeadsetOutputsTheDefaultOneIsUsed() =>
        Assert.Equal(("usb", HeadsetFound.Default), HeadsetOutput.Pick([Dock, Usb], "usb", "dock"));

    [Fact]
    public void NoHeadsetOutputGivesNone() =>
        Assert.Equal((null, HeadsetFound.None), HeadsetOutput.Pick([], "speakers", null));
}
