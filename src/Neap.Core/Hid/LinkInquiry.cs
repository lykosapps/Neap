using Neap.Core.Protocol;

namespace Neap.Core.Hid;

/// <summary>
/// The transmitter's own answer to whether the headset is linked to it: a
/// read the transmitter answers itself, without asking the headset.
/// </summary>
/// <remarks>
/// <para>
/// RACE command 0x9931, the frame SP2Tools proved byte for byte the same as
/// Swarm II's on the Stealth Pro II's dock, and found answered by the
/// transmitter itself in the Atlas Air's firmware (F115). Its reply is
/// <c>07 07 00 05 5b 03 00 31 99 b</c>, with <c>b</c> 1 while the headset is
/// linked and 0 while it is not.
/// </para>
/// <para>
/// It tells apart the two reasons a transmitter gives only empty replies: no
/// headset linked to it, which a restart cannot help, and a control channel
/// that has stopped answering, which only a restart brings back. A
/// transmitter that answers this has a working channel.
/// </para>
/// </remarks>
public static class LinkInquiry
{
    /// <summary>The RACE command id.</summary>
    public const ushort CommandId = 0x9931;

    /// <summary>The request, answered by the transmitter rather than relayed to the headset.</summary>
    public static byte[] Frame => Frames.Command(CommandId, [1, 0, 0, 0], relayThroughBase: false);

    /// <summary>What a report says in answer to the inquiry.</summary>
    /// <remarks>
    /// A reply to the inquiry whose payload is not the one byte 0 or 1 is
    /// unreadable rather than no answer: the transmitter did answer, so its
    /// channel works, whatever the reply means.
    /// </remarks>
    public static LinkAnswer Answer(ReadOnlySpan<byte> report)
    {
        if (!Frames.IsCommandReply(report, CommandId)) return LinkAnswer.None;
        return Frames.CommandReply(report, CommandId) switch
        {
            [1] => LinkAnswer.Linked,
            [0] => LinkAnswer.NotLinked,
            _ => LinkAnswer.Unreadable,
        };
    }
}

/// <summary>A transmitter's answer to <see cref="LinkInquiry"/>.</summary>
public enum LinkAnswer
{
    /// <summary>No reply to the inquiry.</summary>
    None,

    /// <summary>A reply saying no headset is linked.</summary>
    NotLinked,

    /// <summary>A reply saying the headset is linked.</summary>
    Linked,

    /// <summary>A reply to the inquiry that says neither.</summary>
    Unreadable,
}
