using System.Buffers.Binary;
using System.Globalization;
using Neap.Core.Protocol;

namespace Neap.Core.Hid;

/// <summary>
/// What the headset's buttons and switches have done, as the headset keeps
/// it: a word of flags, one for each press and each release, and where each
/// wheel stands.
/// </summary>
/// <remarks>
/// <para>
/// Read for the log, to learn on a tester's headset which button a setting
/// belongs to and what the boom does to the microphone's mute switch. RACE
/// command 0x0000 in read mode returns the flags word and three wheel
/// positions, each a little-endian 32-bit number. A flag stays set once its
/// press or release has happened, so two readings show what changed between
/// them (SP2Tools F126, read from the Atlas Air's firmware).
/// </para>
/// <para>
/// The bits come in pairs, a press then a release: 0 and 1 the power button
/// (key 0x18), 2 and 3 the microphone mute switch (key 0x81), 4 and 5 the
/// source switch (key 0x7e), 6 and 7 the button whose short press
/// 0xB00 to 0xB20 set (key 0x7f), 8 and 9 the media and call button
/// (key 0x80), 10 and 11 the mute switch's second contact (key 0x82); then
/// 16 to 21 each wheel turning up and down.
/// </para>
/// </remarks>
public static class HeadsetInputs
{
    /// <summary>The RACE command id.</summary>
    public const ushort CommandId = 0x0000;

    private const byte ReadMode = 1;

    private static readonly string[] Names =
    [
        "power pressed", "power released",
        "mute switch pressed", "mute switch released",
        "source switch pressed", "source switch released",
        "button 0x7f pressed", "button 0x7f released",
        "media button pressed", "media button released",
        "mute switch's second contact pressed", "mute switch's second contact released",
    ];

    private static readonly string[] WheelNames =
    [
        "wheel 0 up", "wheel 0 down", "wheel 1 up", "wheel 1 down", "wheel 2 up", "wheel 2 down",
    ];

    /// <summary>The request, relayed to the headset when it sits behind a transmitter.</summary>
    public static byte[] ReadFrame(bool relayThroughBase) => Frames.Command(CommandId, [ReadMode], relayThroughBase);

    /// <summary>The flags and wheel positions in a reply to <see cref="ReadFrame"/>, or null when the report is not one.</summary>
    public static InputReading? InReply(ReadOnlySpan<byte> report)
    {
        var payload = Frames.CommandReply(report, CommandId);
        if (payload.Length != 16) return null;
        return new InputReading(
            BinaryPrimitives.ReadUInt32LittleEndian(payload),
            BinaryPrimitives.ReadUInt32LittleEndian(payload[4..]),
            BinaryPrimitives.ReadUInt32LittleEndian(payload[8..]),
            BinaryPrimitives.ReadUInt32LittleEndian(payload[12..]));
    }

    /// <summary>The presses, releases and turns a flags word holds, in words, with any bit not known by its number.</summary>
    public static IReadOnlyList<string> Describe(uint flags)
    {
        var said = new List<string>();
        for (int bit = 0; bit < 32; bit++)
        {
            if ((flags & (1u << bit)) == 0) continue;
            said.Add(bit < Names.Length ? Names[bit]
                : bit is >= 16 and < 22 ? WheelNames[bit - 16]
                : string.Create(CultureInfo.InvariantCulture, $"bit {bit}"));
        }
        return said;
    }
}

/// <summary>One reading of the headset's inputs: the flags word and where each of three wheels stands.</summary>
public readonly record struct InputReading(uint Flags, uint Wheel0, uint Wheel1, uint Wheel2);
