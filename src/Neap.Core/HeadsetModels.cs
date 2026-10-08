using System.Globalization;
using Neap.Core.Diagnostics;
using static Neap.Core.Transmitters;

namespace Neap.Core;

/// <summary>A headset Neap knows, and what it knows of it.</summary>
/// <param name="Name">The name on the box, for what a person reads.</param>
/// <param name="SoundName">The words its speakers and microphone carry in their names on the PC, lowercase.</param>
/// <param name="Writable">Whether changing its settings has been confirmed on the headset; until it has, Neap only reads them.</param>
/// <param name="Hardware">Its headset, docks and transmitters, by USB product id.</param>
/// <param name="Features">What it has of what Neap does.</param>
public sealed record HeadsetModel(
    string Name, string SoundName, bool Writable,
    IReadOnlyDictionary<string, Piece> Hardware, IReadOnlySet<Feature> Features);

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
        Enum.GetValues<Feature>().ToHashSet());

    /// <summary>The Atlas Air, known from one owner's recording and its product page.</summary>
    /// <remarks>
    /// <para>
    /// Its transmitter is 225E and the headset reports itself as 2260, from a
    /// recording an owner sent. It answered for the same settings, under the
    /// same keys, as the Stealth Pro II, so its values are read as the Stealth
    /// Pro II's. Nothing is written until an owner confirms what each one does.
    /// </para>
    /// <para>
    /// It has Superhuman Hearing, by its product page, but gave no answer for
    /// where the Stealth Pro II keeps it, so it is left out until it can be
    /// read. It has no chat wheel, noise cancellation, lights or dock.
    /// </para>
    /// </remarks>
    public static readonly HeadsetModel AtlasAir = new(
        "Atlas Air", "atlas air", Writable: false,
        new Dictionary<string, Piece>(StringComparer.OrdinalIgnoreCase)
        {
            ["225E"] = Piece.Transmitter,
            ["2260"] = Piece.Headset,
        },
        new HashSet<Feature>
        {
            Feature.Battery, Feature.MasterVolume, Feature.GameEqualiser, Feature.Microphone,
            Feature.MicMonitoring, Feature.AiNoiseReduction, Feature.MicrophoneEqualiser,
            Feature.ModeButton, Feature.LowerDial, Feature.AutoShutOff,
        });

    public static readonly IReadOnlyList<HeadsetModel> All = [StealthProII, AtlasAir];

    /// <summary>Every known headset's <see cref="HeadsetModel.SoundName"/>, apart by |, for finding its speakers and microphone.</summary>
    /// <remarks>Written out rather than built from <see cref="All"/>, as it is the default of many a parameter; a test keeps the two the same.</remarks>
    public const string SoundNames = "stealth pro|atlas air";

    /// <summary>Whether a speaker or microphone's name holds any of the fragments in <paramref name="match"/>, apart by |.</summary>
    public static bool SoundMatches(string name, string match = SoundNames) =>
        match.Split('|').Any(part => name.Contains(part, StringComparison.OrdinalIgnoreCase));

    /// <summary>The headset a piece of hardware belongs to, by its USB product id, or null for one Neap does not know.</summary>
    public static HeadsetModel? Of(ushort product) =>
        Of(product.ToString("X4", CultureInfo.InvariantCulture));

    /// <inheritdoc cref="Of(ushort)"/>
    public static HeadsetModel? Of(string product) =>
        All.FirstOrDefault(model => model.Hardware.ContainsKey(product));
}
