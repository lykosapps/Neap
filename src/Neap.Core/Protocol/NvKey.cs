using System.Buffers.Binary;

namespace Neap.Core.Protocol;

/// <summary>
/// RACE commands 0x0A00 and 0x0A01: reads and writes a stored settings record
/// by its key, in frames longer than one HID report.
/// </summary>
/// <remarks>
/// A record is saved by the headset and is not a <c>set_kvp</c> setting, so
/// only the records Neap names are ever written: the settings registry's
/// guarantee, kept by naming each key in its own class
/// (<see cref="Presets.GameEqRecord"/>). A frame is cut into reports of at most
/// 59 bytes, each with its own length, and the reply comes back the same way
/// among unrelated notifications.
/// </remarks>
public static class NvKey
{
    /// <summary>The RACE command that reads a record.</summary>
    public const ushort ReadCommand = 0x0A00;

    /// <summary>The RACE command that writes a record.</summary>
    public const ushort WriteCommand = 0x0A01;

    private const int Chunk = 59;
    private const byte RaceMarker = 0x05;
    private const byte RaceCommandByte = 0x5A;

    /// <summary>The reports that read a record.</summary>
    /// <param name="key">The record's key.</param>
    /// <param name="length">How many bytes the record is.</param>
    /// <param name="relayThroughBase">True for a dock or transmitter, false on the wired headset.</param>
    public static IReadOnlyList<byte[]> ReadReports(ushort key, ushort length, bool relayThroughBase)
    {
        var payload = new byte[4];
        BinaryPrimitives.WriteUInt16LittleEndian(payload, key);
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(2), length);
        return Reports(ReadCommand, payload, relayThroughBase);
    }

    /// <summary>The reports that write a whole record.</summary>
    /// <param name="key">The record's key.</param>
    /// <param name="data">The record's bytes.</param>
    /// <param name="relayThroughBase">True for a dock or transmitter, false on the wired headset.</param>
    public static IReadOnlyList<byte[]> WriteReports(ushort key, ReadOnlySpan<byte> data, bool relayThroughBase)
    {
        var payload = new byte[2 + data.Length];
        BinaryPrimitives.WriteUInt16LittleEndian(payload, key);
        data.CopyTo(payload.AsSpan(2));
        return Reports(WriteCommand, payload, relayThroughBase);
    }

    private static List<byte[]> Reports(ushort command, byte[] payload, bool relayThroughBase)
    {
        var frame = new byte[6 + payload.Length];
        frame[0] = RaceMarker;
        frame[1] = RaceCommandByte;
        BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(2), (ushort)(payload.Length + 2));
        BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(4), command);
        payload.CopyTo(frame.AsSpan(6));

        var reports = new List<byte[]>();
        for (int at = 0; at < frame.Length; at += Chunk)
        {
            int length = Math.Min(Chunk, frame.Length - at);
            var report = new byte[Frames.ReportLength];
            report[0] = Frames.OutReportId;
            report[1] = (byte)length;
            report[2] = relayThroughBase ? (byte)0x80 : (byte)0;
            frame.AsSpan(at, length).CopyTo(report.AsSpan(3));
            reports.Add(report);
        }

        return reports;
    }
}

/// <summary>Gathers the reports of a record read and finds the record in them.</summary>
/// <remarks>
/// The record comes back in pieces among notifications that are not for it, so
/// every input report's data is joined in order and the record is looked for
/// by its length and its first bytes.
/// </remarks>
public sealed class NvKeyReader
{
    private readonly ushort _length;
    private readonly List<byte> _stream = [];

    /// <param name="length">How many bytes the record is.</param>
    public NvKeyReader(ushort length) => _length = length;

    /// <summary>Takes in one input report.</summary>
    /// <param name="report">The report as the headset sent it.</param>
    public void Add(ReadOnlySpan<byte> report)
    {
        if (report.Length < 3 || report[0] != Frames.InReportId) return;
        int length = Math.Min(report[1] | ((report[2] & 0x7F) << 8), report.Length - 3);
        _stream.AddRange(report.Slice(3, length).ToArray());
    }

    /// <summary>The record, or null when what has arrived does not yet hold all of it.</summary>
    /// <param name="start">The record's first bytes, to tell it from anything else.</param>
    public byte[]? Record(ReadOnlySpan<byte> start)
    {
        var pattern = new byte[2 + start.Length];
        BinaryPrimitives.WriteUInt16LittleEndian(pattern, _length);
        start.CopyTo(pattern.AsSpan(2));

        var seen = System.Runtime.InteropServices.CollectionsMarshal.AsSpan(_stream);
        int at = seen.IndexOf(pattern);
        if (at < 0 || seen.Length < at + 2 + _length) return null;
        return seen.Slice(at + 2, _length).ToArray();
    }
}
