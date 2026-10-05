using System.Text.Json;
using Neap.Core.Audio;
using Neap.Core.Diagnostics;
using Neap.Core.Hid;
using Neap.Core.Protocol;

namespace Neap.Core.Tests.Diagnostics;

public class RecordingTests
{
    private static readonly DateTime Start = new(2026, 10, 5, 14, 32, 0, DateTimeKind.Local);

    private static JsonElement Json(string raw) => JsonDocument.Parse(raw).RootElement;

    private static Dictionary<string, JsonElement> Values(params (string Key, string Raw)[] pairs) =>
        pairs.ToDictionary(p => p.Key, p => Json(p.Raw));

    private static readonly Transmitter Dock = Transmitters.Describe(2, Json(
        """{"info":["2","17","1","100","1","10F5","229B","4.107.703.0","AA:BB:CC:DD:EE:FF"],"control":["0","80","30"]}"""));

    private static readonly SoundSurvey Sound = new(
        [new(true, "Multimedia", "Headset (Charging Dock)", "229B")],
        [new(true, "Headset (Charging Dock)", "229B", 45, false, 0.12f,
            [new("Discord", 100, false, true), new("witcher3", 60, false, false)])],
        "24-bit, 48 kHz (Studio quality)", "16-bit, 16 kHz (Telephone quality)");

    private static Snapshot At(DateTime at, Dictionary<string, JsonElement> values, SoundSurvey? sound = null,
        IReadOnlyList<string>? unread = null) =>
        new(at, "Connected via Charging Dock", [new HidDeviceInfo("", 0x10F5, 0x229B, 0xFF13, 64, 0, 64)],
            [Dock, Transmitters.Describe(1, default)], values, sound ?? Sound, unread ?? []);

    private static string Written(Recording recording) =>
        recording.Write(["2026-10-05 14:31:58.2  headset: Connected via Charging Dock"], new Redaction(recording.Secrets));

    [Fact]
    public void TheSerialAndRadioAddressesAreNotInTheText()
    {
        var recording = new Recording("Neap 0.1.0", "10.0.26200");
        recording.Began(At(Start, Values(("120", "\"TB2286X0042\""))));
        recording.Ended(At(Start.AddSeconds(5), Values(("120", "\"TB2286X0042\""))));

        string text = Written(recording);

        Assert.DoesNotContain("TB2286X0042", text, StringComparison.Ordinal);
        Assert.DoesNotContain("AA:BB:CC:DD:EE:FF", text, StringComparison.Ordinal);
        Assert.Contains("serial_number", text, StringComparison.Ordinal);
    }

    [Fact]
    public void AValueNobodyHasNamedIsKeptAndMarked()
    {
        var recording = new Recording("Neap", "10");
        recording.Began(At(Start, Values(("610", "3"), ("6ff", "17"))));

        string text = Written(recording);

        Assert.Contains("Headset values: 2, 1 not yet identified", text, StringComparison.Ordinal);
        Assert.Matches(@"0x6ff\s+not yet identified\s+17", text);
    }

    [Fact]
    public void WhatTheHeadsetSaysIsKeptInTimeOrderWithTheLevels()
    {
        var recording = new Recording("Neap", "10");
        recording.Began(At(Start, Values()));
        recording.Heard(Start.AddSeconds(1), new DeviceEvent("UP", "Mic", Values(("610", "4"))));
        recording.Sampled(Start.AddSeconds(2), Sound);

        string[] during = Section(Written(recording), "During");

        Assert.StartsWith("14:32:01.0  headset  UP/Mic  0x610", during[0], StringComparison.Ordinal);
        Assert.Equal("14:32:02.0  sound    output Headset (Charging Dock) [Charging Dock 229B]  45%  peak 0.120"
            + "  apps: Discord 100% playing, witcher3 60%", during[1]);
    }

    [Fact]
    public void AMoveOfWindowsDefaultIsNoted()
    {
        var recording = new Recording("Neap", "10");
        recording.Began(At(Start, Values()));
        recording.Sampled(Start.AddSeconds(1), Sound with
        {
            Defaults = [new(true, "Multimedia", "Speakers (Realtek)", "")],
        });

        Assert.Contains(Section(Written(recording), "During"),
            l => l.Contains("default  output Multimedia     Speakers (Realtek)  [not the headset]", StringComparison.Ordinal));
    }

    [Fact]
    public void TheEndSaysOnlyWhatChanged()
    {
        var recording = new Recording("Neap", "10");
        recording.Began(At(Start, Values(("610", "3"), ("620", "1"), ("630", "0"))));
        recording.Ended(At(Start.AddSeconds(70), Values(("610", "4"), ("620", "1"), ("640", "2"))));

        string text = Written(recording);
        string[] end = Section(text, "At the end");

        Assert.Contains("Ended: 2026-10-05 14:33:10, 1 min 10 s", text, StringComparison.Ordinal);
        Assert.Contains(end, l => l.Contains("0x610") && l.EndsWith("4  (was 3)", StringComparison.Ordinal));
        Assert.Contains(end, l => l.Contains("0x640") && l.EndsWith("(new)", StringComparison.Ordinal));
        Assert.Contains(end, l => l.Contains("0x630") && l.EndsWith("no longer reported", StringComparison.Ordinal));
        Assert.DoesNotContain(end, l => l.Contains("0x620", StringComparison.Ordinal));
    }

    [Fact]
    public void WhatCouldNotBeReadIsSaid()
    {
        var recording = new Recording("Neap", "10");
        recording.Began(At(Start, Values(), unread: ["headset values: the headset is not answering"]));
        recording.Failed(Start.AddSeconds(1), "Windows sound: no audio device available");

        string text = Written(recording);

        Assert.Contains("Could not read:\n  headset values: the headset is not answering",
            text.ReplaceLineEndings("\n"), StringComparison.Ordinal);
        Assert.Contains("failed   Windows sound: no audio device available", text, StringComparison.Ordinal);
    }

    [Fact]
    public void TheTimelineStopsGrowingAndSaysSo()
    {
        var recording = new Recording("Neap", "10");
        for (int i = 0; i < Recording.MostLines + 10; i++)
            recording.Failed(Start, "nothing");

        string[] during = Section(Written(recording), "During");

        Assert.Equal(Recording.MostLines + 1, during.Length);
        Assert.EndsWith($"the recording reached {Recording.MostLines} lines", during[^1], StringComparison.Ordinal);
    }

    [Fact]
    public void TheLogIsIncluded()
    {
        var recording = new Recording("Neap", "10");

        Assert.Equal(["2026-10-05 14:31:58.2  headset: Connected via Charging Dock"], Section(Written(recording), "App log"));
    }

    [Fact]
    public void FindingsCountWhatTheHeadsetSaidAndWhatChangedByTheEnd()
    {
        var recording = new Recording("Neap", "10");
        recording.Began(At(Start, Values(("240", "76"), ("2a0", "45"))));
        recording.Heard(Start.AddSeconds(1), new DeviceEvent("UP", "3DT", Values(("510", "7"))));
        recording.Ended(At(Start.AddSeconds(2), Values(("240", "76"), ("2a0", "50"))));

        var findings = recording.Findings();

        Assert.Contains(Feature.ChatWheel, findings.Found);
        Assert.Equal([Feature.MasterVolume, Feature.ChatWheel], findings.Moved);
    }

    [Fact]
    public void OnlyARequestToSupportAnotherHeadsetCarriesTheFindings()
    {
        static Snapshot With(ushort product) => new(Start, "Quiet", [new HidDeviceInfo("", 0x10F5, product, 0xFF13, 64, 0, 64)],
            [], Values(("240", "76")), null, []);

        var known = new Recording("Neap", "10");
        known.Began(With(0x229B));
        var other = new Recording("Neap", "10");
        other.Began(With(0x2201));

        Assert.False(known.ForAnotherHeadset);
        Assert.DoesNotContain("found=", known.Issue().AbsoluteUri, StringComparison.Ordinal);
        Assert.True(other.ForAnotherHeadset);
        Assert.Contains("found=Found%3A%20Battery", other.Issue().AbsoluteUri, StringComparison.Ordinal);
    }

    /// <summary>The lines under one heading, up to the blank line before the next.</summary>
    private static string[] Section(string text, string heading) =>
        text.ReplaceLineEndings("\n").Split("\n")
            .SkipWhile(l => l != $"== {heading} ==").Skip(1)
            .TakeWhile(l => l.Length > 0).ToArray();
}
