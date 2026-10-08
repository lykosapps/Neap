using System.Globalization;
using Neap.Core.Diagnostics;
using static Neap.Core.Transmitters;

namespace Neap.Core;

/// <summary>A control on a headset that does one thing, which Neap describes rather than sets.</summary>
public enum FixedControl
{
    VolumeWheel,

    /// <summary>The microphone mutes when flipped up.</summary>
    FlipToMute,

    /// <summary>Switches between the transmitters it is paired with.</summary>
    CrossPlay,

    /// <summary>The Stealth Pro II's: Bluetooth on and off, and pairing.</summary>
    BluetoothButton,

    /// <summary>Switches between the transmitter and Bluetooth.</summary>
    QuickSwitch,

    /// <summary>The Atlas Air's: pairing, and answering and ending calls.</summary>
    BluetoothCallButton,
}

/// <summary>A headset Neap knows, and what it knows of it.</summary>
/// <param name="Name">The name on the box, for what a person reads.</param>
/// <param name="SoundName">The words its speakers and microphone carry in their names on the PC, lowercase.</param>
/// <param name="Writable">Whether changing its settings has been confirmed on the headset; until it has, Neap only reads them.</param>
/// <param name="Hardware">Its headset, docks and transmitters, by USB product id.</param>
/// <param name="Features">What it has of what Neap does.</param>
/// <param name="Unread">Of <paramref name="Features"/>, those Neap cannot yet read on it, offered without a reading so they can be tried.</param>
/// <param name="Controls">Its controls that do one thing, in the order they are listed.</param>
public sealed record HeadsetModel(
    string Name, string SoundName, bool Writable,
    IReadOnlyDictionary<string, Piece> Hardware, IReadOnlySet<Feature> Features,
    IReadOnlySet<Feature> Unread, IReadOnlyList<FixedControl> Controls)
{
    /// <summary>Whether it pairs with several transmitters, a Charging Dock among them, and switches between them with its CrossPlay button.</summary>
    public bool CrossPlay => Controls.Contains(FixedControl.CrossPlay);
}

/// <summary>The headsets Neap knows.</summary>
public static class HeadsetModels
{
    /// <summary>The headset Neap is built for, whose every setting has been confirmed on it.</summary>
    /// <remarks>
    /// <para>
    /// The ids come out of Swarm II's own product catalogue rather than being
    /// guessed at. The catalogue is in Swarm's settings.json, zlib-compressed
    /// behind a four-byte header. It is the authority for which id is which;
    /// the order of firmware folders is not. It confirms 229B as the white Xbox
    /// dock and 229D as its transmitter, and names 2283 as the black Xbox dock.
    /// </para>
    /// <para>
    /// Swarm calls the docking device a base; the box calls it a "CrossPlay
    /// 2.0 Transmitter Dock". The box wins for anything a person reads,
    /// because that is the word they were sold.
    /// </para>
    /// </remarks>
    public static readonly HeadsetModel StealthProII = new(
        "Stealth Pro II", "stealth pro", Writable: true,
        new Dictionary<string, Piece>(StringComparer.OrdinalIgnoreCase)
        {
            // Xbox, black
            ["2283"] = Piece.Dock,
            ["2284"] = Piece.Dock,
            ["2285"] = Piece.Transmitter,
            ["2286"] = Piece.Headset,
            ["229F"] = Piece.Transmitter,
            // Xbox, white
            ["229B"] = Piece.Dock,
            ["229C"] = Piece.Dock,
            ["229D"] = Piece.Transmitter,
            ["229E"] = Piece.Headset,
            ["22A0"] = Piece.Transmitter,
            // PC
            ["2287"] = Piece.Dock,
            ["2288"] = Piece.Transmitter,
            ["2289"] = Piece.Headset,
        },
        Enum.GetValues<Feature>().ToHashSet(),
        new HashSet<Feature>(),
        [FixedControl.VolumeWheel, FixedControl.FlipToMute, FixedControl.CrossPlay, FixedControl.BluetoothButton]);

    /// <summary>The Atlas Air, from one owner's recording and Turtle Beach's support pages for it, in testing.</summary>
    /// <remarks>
    /// <para>
    /// Its transmitter is 225E and the headset reports itself as 2260, from a
    /// recording an owner sent. It answered for the same settings, under the
    /// same keys, as the Stealth Pro II, so its values are read and written as
    /// the Stealth Pro II's. Every function is on while its tester confirms each
    /// one; a public release carries only those confirmed, as DECISIONS.md says.
    /// </para>
    /// <para>
    /// What it has is from Turtle Beach's support articles for it: the Swarm II
    /// app's desktop and mobile versions, and the quick start guide, which is
    /// the word on its physical controls (see FINDINGS.md). It has no chat
    /// wheel, Mode button, second wheel, noise cancellation, wake on motion or
    /// dock. Superhuman Hearing, the noise gate, voice prompts and the
    /// transmitter's light gave no reading in the owner's recording, so they
    /// are offered unread for the tester to try.
    /// </para>
    /// </remarks>
    public static readonly HeadsetModel AtlasAir = new(
        "Atlas Air", "atlas air", Writable: true,
        new Dictionary<string, Piece>(StringComparer.OrdinalIgnoreCase)
        {
            ["225E"] = Piece.Transmitter,
            ["2260"] = Piece.Headset,
        },
        new HashSet<Feature>
        {
            Feature.Battery, Feature.MasterVolume, Feature.GameEqualiser, Feature.Microphone,
            Feature.MicMonitoring, Feature.AiNoiseReduction, Feature.MicrophoneEqualiser,
            Feature.AutoShutOff, Feature.SuperhumanHearing, Feature.NoiseGate,
            Feature.VoicePrompts, Feature.Lights,
        },
        new HashSet<Feature> { Feature.SuperhumanHearing, Feature.NoiseGate, Feature.VoicePrompts, Feature.Lights },
        [FixedControl.VolumeWheel, FixedControl.FlipToMute, FixedControl.QuickSwitch, FixedControl.BluetoothCallButton]);

    public static readonly IReadOnlyList<HeadsetModel> All = [StealthProII, AtlasAir];

    /// <summary>Every known headset's <see cref="HeadsetModel.SoundName"/>, apart by |, for finding its speakers and microphone.</summary>
    /// <remarks>Written out rather than built from <see cref="All"/>, as it is the default of many a parameter; a test keeps the two the same.</remarks>
    public const string SoundNames = "stealth pro|atlas air";

    /// <summary>Whether a speaker or microphone's name holds any of the fragments in <paramref name="match"/>, apart by |.</summary>
    public static bool SoundMatches(string name, string match = SoundNames) =>
        match.Split('|').Any(part => name.Contains(part, StringComparison.OrdinalIgnoreCase));

    /// <summary>Whether a function is shown for a headset: any is while the headset is not known, as it was before Neap knew more than one.</summary>
    public static bool Shows(HeadsetModel? model, Feature feature) =>
        model is null || model.Features.Contains(feature);

    /// <summary>Whether what goes with CrossPlay is shown: the dock, its ring and switching transmitters; see <see cref="HeadsetModel.CrossPlay"/>.</summary>
    public static bool ShowsCrossPlay(HeadsetModel? model) => model is null || model.CrossPlay;

    /// <summary>The controls that do one thing to list for a headset: the Stealth Pro II's while it is not known.</summary>
    public static IReadOnlyList<FixedControl> ControlsOf(HeadsetModel? model) => (model ?? StealthProII).Controls;

    /// <summary>Whether a setting is offered without a reading, as one of the functions a headset has that Neap cannot yet read.</summary>
    public static bool Unread(HeadsetModel? model, string setting) =>
        model is not null && HeadsetCheck.Needs.Any(need => need.Value.Contains(setting) && model.Unread.Contains(need.Key));

    /// <summary>Whether a setting is shown for a headset: one that belongs to no function always is; see <see cref="HeadsetCheck.Needs"/>.</summary>
    public static bool Shows(HeadsetModel? model, string setting) =>
        HeadsetCheck.Needs.Where(need => need.Value.Contains(setting)).All(need => Shows(model, need.Key));

    /// <summary>The headset a piece of hardware belongs to, by its USB product id, or null for one Neap does not know.</summary>
    public static HeadsetModel? Of(ushort product) =>
        Of(product.ToString("X4", CultureInfo.InvariantCulture));

    /// <inheritdoc cref="Of(ushort)"/>
    public static HeadsetModel? Of(string product) =>
        All.FirstOrDefault(model => model.Hardware.ContainsKey(product));
}
