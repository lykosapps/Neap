using System.Text;
using System.Text.RegularExpressions;
using StealthPro.Core.Protocol;
using StealthPro.Core.Settings;

namespace StealthPro.Probe;

/// <summary>
/// Reads a USBPcap recording of Swarm II driving the headset and prints what
/// was said in both directions.
/// </summary>
/// <remarks>
/// <para>
/// Every command in the registry was found by watching Swarm II send it. The
/// probe can only ask the headset about what is already known; a capture is
/// the only way to see a new command, as a new firmware, a different edition
/// of the headset or an unexplored part of Swarm II needs. This decodes the
/// files the capture scripts in tools/ produce.
/// </para>
/// <para>
/// Make a capture with, as administrator:
/// <code>
/// USBPcapCMD.exe -d \\.\USBPcap&lt;n&gt; -o out.pcap --devices &lt;addr&gt; --inject-descriptors
/// </code>
/// Use a short output path. USBPcapCMD fails past the Windows path limit and
/// says only "Thread started with invalid write handle".
/// </para>
/// </remarks>
internal static class Capture
{
    private const byte SetupBmRequestType = 0x21;
    private const byte SetupRequest = 0x09;
    private const byte StageSetup = 0;
    private const byte StageComplete = 3;
    private const byte InEndpoint = 0x80;

    /// <summary>Matches a verb and its optional JSON argument.</summary>
    /// <remarks>
    /// Verbs can end in a digit: SCG1..SCG5 are the five custom equaliser
    /// slots and STX1..STX4 the four transmitters. A letters-only pattern
    /// truncates them to SCG and STX, so different commands look identical.
    /// </remarks>
    private static readonly Regex Command =
        new("([A-Za-z_][A-Za-z0-9_]{2,19})(?:\u00ff(\\{.*?\\}))?", RegexOptions.Singleline);

    private static readonly Regex KeyInArgs = new("\"0x([0-9a-fA-F]+)\"");

    private readonly record struct Packet(
        ushort Device, byte Endpoint, byte Stage, byte[] Data);

    public static int Decode(string path, int address)
    {
        if (!File.Exists(path))
        {
            Console.Error.WriteLine($"no such capture: {path}");
            return 2;
        }

        var lines = new List<string>();
        void Say(string line = "")
        {
            Console.WriteLine(line);
            lines.Add(line);
        }

        var rows = Records(File.ReadAllBytes(path))
            .Where(r => r.Device == address).ToList();
        if (rows.Count == 0)
        {
            Console.WriteLine($"no traffic for device address {address} in {path}");
            Console.WriteLine("Addresses present: " + string.Join(", ",
                Records(File.ReadAllBytes(path)).Select(r => r.Device).Distinct().Order()));
            return 1;
        }

        Say($"{Path.GetFileName(path)} — device address {address}, {rows.Count} packets");
        Say();
        Say(new string('=', 70));
        Say("COMMANDS SENT BY SWARM II");
        Say(new string('=', 70));

        int commands = 0;
        foreach (var row in rows)
        {
            if (row.Endpoint != 0 || row.Stage != StageSetup || row.Data.Length < 9) continue;
            if (row.Data[0] != SetupBmRequestType || row.Data[1] != SetupRequest) continue;

            commands++;
            string body = Latin1(row.Data, 8);
            var match = Command.Match(body, Math.Min(20, body.Length));
            if (!match.Success)
            {
                Say("  ? " + Convert.ToHexString(row.Data.AsSpan(8)).ToLowerInvariant().TrimEnd('0'));
                continue;
            }
            if (!match.Groups[2].Success)
            {
                Say($"  {match.Groups[1].Value,-8} (no args)");
                continue;
            }

            string args = match.Groups[2].Value;
            string tags = string.Concat(KeyInArgs.Matches(args)
                .Select(m => Label(m.Groups[1].Value)));
            Say($"  {match.Groups[1].Value,-8} {args}{tags}");
        }
        if (commands == 0)
            Say("  (none — Swarm II sent no settings changes during this capture)");

        Say();
        Say(new string('=', 70));
        Say("EVENTS FROM THE HEADSET");
        Say(new string('=', 70));

        var buffer = new List<byte>();
        var seen = new HashSet<string>();
        foreach (var row in rows)
        {
            if (row.Endpoint != InEndpoint || row.Stage != StageComplete) continue;
            var payload = Frames.PayloadOf(row.Data);
            if (payload.Length == 0) continue;

            buffer.AddRange(payload);
            var (events, remainder) = EventParser.Consume(buffer.ToArray());
            buffer.Clear();
            buffer.AddRange(remainder.Length > 8192 ? remainder[^8192..] : remainder);

            foreach (var evt in events)
            {
                string text = evt.ToString();
                if (!seen.Add(text)) continue;

                // Only short events are annotated inline. A fifteen-value
                // block is unreadable with a tag after every field, so its
                // unnamed keys are listed underneath instead.
                if (evt.Values.Count <= 3)
                {
                    Say($"  {text}" + string.Concat(evt.Values.Keys.Select(Label)));
                    continue;
                }

                Say($"  {text}");
                var unnamed = evt.Values.Keys.Where(k => !Named(k)).ToList();
                if (unnamed.Count > 0)
                    Say("      unnamed in this block: "
                        + string.Join(", ", unnamed.Select(k => "0x" + k)));
            }
        }
        if (seen.Count == 0) Say("  (none)");

        // Anything event-shaped that would not decode, so that "cannot parse"
        // is never mistaken for "does not exist".
        if (EventParser.Unrecognised.Count > 0)
        {
            Say();
            Say("event-shaped but not decoded:");
            foreach (var fragment in EventParser.Unrecognised) Say($"  {fragment}");
        }

        string target = Path.ChangeExtension(path, null) + "-decoded.txt";
        File.WriteAllLines(target, lines);
        Console.WriteLine($"\nsaved -> {target}");
        return 0;
    }

    /// <summary>
    /// Walks the pcap: a 24-byte file header, then a 16-byte header before each record.
    /// </summary>
    private static IEnumerable<Packet> Records(byte[] data)
    {
        int offset = 24;
        while (offset + 16 <= data.Length)
        {
            int captured = BitConverter.ToInt32(data, offset + 8);
            offset += 16;
            if (captured < 0 || offset + captured > data.Length) yield break;

            var packet = data.AsSpan(offset, captured);
            offset += captured;
            if (packet.Length < 27) continue;

            int headerLength = BitConverter.ToUInt16(packet[..2]);
            int dataLength = BitConverter.ToInt32(packet[23..27]);
            if (headerLength > packet.Length || dataLength < 0) continue;

            int available = Math.Min(dataLength, packet.Length - headerLength);
            yield return new Packet(
                BitConverter.ToUInt16(packet[19..21]),
                packet[21],
                headerLength > 27 ? packet[27] : (byte)0xFF,
                packet.Slice(headerLength, available).ToArray());
        }
    }

    /// <summary>
    /// Decodes as Latin-1, so byte offsets and character offsets match; the
    /// command pattern indexes into the result.
    /// </summary>
    private static string Latin1(byte[] data, int from) =>
        Encoding.Latin1.GetString(data, from, data.Length - from);

    private static bool Named(string key)
    {
        try { _ = Registry.Resolve(key); return true; }
        catch (KeyNotFoundException) { return false; }
    }

    private static string Label(string key)
    {
        try { return $"  [{Registry.Resolve(key).Name}]"; }
        catch (KeyNotFoundException) { return "  <- UNNAMED"; }
    }
}
