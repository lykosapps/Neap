using System.Globalization;
using System.Text;

namespace StealthPro.Core.Protocol;

public class ProtocolException : Exception
{
    public ProtocolException(string message) : base(message) { }
}

/// <summary>
/// Frame construction. Turtle Beach layer a text protocol over Airoha's RACE
/// transport; every frame seen so far looks like this:
///
/// <code>
///   06              HID output report id
///   LL LL           total length, little-endian, from the next byte
///   05              RACE start
///   5A              command
///   ll ll           inner length, little-endian, from the next byte
///   02 99           RACE command id 0x9902 (Turtle Beach vendor command)
///   48              constant
///   01 / 03         01 = verb with no argument, 03 = verb with an argument
///   FB c1 c2        message counter, not validated by the device
///   00 x7           padding
///   &lt;tag&gt;&lt;verb&gt;     tag byte differs per verb and is not yet understood
///   [FF &lt;json&gt;]     argument, when present
/// </code>
///
/// All of it derived from captured traffic. Ported from
/// stealthpro/protocol.py; see FINDINGS.md for how it was worked out.
/// </summary>
public static class Frames
{
    public const byte RaceStart = 0x05;
    public const byte RaceCommand = 0x5A;
    public const byte OutReportId = 0x06;
    public const byte InReportId = 0x07;

    private static readonly byte[] RaceCommandId = { 0x02, 0x99 };
    private static readonly byte[] ReadTag = { 0x61, 0x00 };
    private static readonly byte[] SetTag = { 0xB7 };

    /// <summary>The output report length both control collections declare.</summary>
    public const int ReportLength = 62;

    public static byte[] Build(
        string verb, IReadOnlyDictionary<string, string>? argument = null, int counter = 0)
    {
        bool takesArgument = Verbs.TakesArgument(verb);
        byte[] tag = Verbs.Tag(verb);

        if (takesArgument && argument is null)
            throw new ProtocolException($"verb '{verb}' requires an argument");
        if (!takesArgument && argument is not null)
            throw new ProtocolException($"verb '{verb}' takes no argument");

        var body = new List<byte>();
        body.AddRange(RaceCommandId);
        body.Add(0x48);
        body.Add(takesArgument ? (byte)0x03 : (byte)0x01);
        body.Add(0xFB);
        body.Add((byte)((counter >> 8) & 0xFF));
        body.Add((byte)(counter & 0xFF));
        body.AddRange(new byte[7]);
        body.AddRange(tag);
        body.AddRange(Encoding.ASCII.GetBytes(verb));
        if (takesArgument)
        {
            body.Add(0xFF);
            body.AddRange(Encoding.ASCII.GetBytes(Encode(argument!)));
        }

        var frame = new List<byte> { OutReportId };
        int inner = body.Count;
        int total = 4 + inner;              // RACE header is 4 bytes
        frame.Add((byte)(total & 0xFF));
        frame.Add((byte)((total >> 8) & 0xFF));
        frame.Add(RaceStart);
        frame.Add(RaceCommand);
        frame.Add((byte)(inner & 0xFF));
        frame.Add((byte)((inner >> 8) & 0xFF));
        frame.AddRange(body);
        return frame.ToArray();
    }

    /// <summary>Frame for <c>set_kvp {"0x&lt;key&gt;":"&lt;value&gt;"}</c>.</summary>
    public static byte[] SetKey(int key, object value, int counter = 0) =>
        Build("set_kvp",
              new Dictionary<string, string>
              {
                  // The headset reads "-30", never a language's own minus sign.
                  [$"0x{key:x}"] = Convert.ToString(value, CultureInfo.InvariantCulture) ?? "",
              },
              counter);

    /// <summary>
    /// The argument object, byte for byte as Python's
    /// <c>json.dumps(separators=(",", ":"))</c> writes it.
    ///
    /// System.Text.Json cannot be used here, and the reason is not obvious.
    /// Its encoder escapes <c>&amp;</c>, <c>&lt;</c>, <c>&gt;</c>, <c>'</c>
    /// and <c>+</c> as \uXXXX for HTML safety — six bytes where Python sends
    /// one. The headset stores what it is given, so a preset named
    /// "Bass &amp; Treble Boost" went out both longer than the report could
    /// hold and spelled wrong. Nothing here is ever rendered as HTML.
    ///
    /// Anything outside printable ASCII still goes out as \uXXXX, which is
    /// what ensure_ascii does and what keeps the frame ASCII-encodable.
    /// </summary>
    private static string Encode(IReadOnlyDictionary<string, string> values)
    {
        var json = new StringBuilder("{");
        bool first = true;
        foreach (var pair in values)
        {
            if (!first) json.Append(',');
            first = false;
            EncodeString(json, pair.Key);
            json.Append(':');
            EncodeString(json, pair.Value);
        }
        return json.Append('}').ToString();
    }

    private static void EncodeString(StringBuilder json, string value)
    {
        json.Append('"');
        foreach (char c in value)
            switch (c)
            {
                case '"': json.Append("\\\""); break;
                case '\\': json.Append("\\\\"); break;
                case '\n': json.Append("\\n"); break;
                case '\r': json.Append("\\r"); break;
                case '\t': json.Append("\\t"); break;
                case '\b': json.Append("\\b"); break;
                case '\f': json.Append("\\f"); break;
                default:
                    if (c is < ' ' or > '~') json.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                    else json.Append(c);
                    break;
            }
        json.Append('"');
    }

    /// <summary>
    /// Strip the report id and length from an inbound report.
    ///
    /// The inbound length is 16-bit little-endian, exactly like outbound.
    /// Reading it as a single byte truncates every fragment by one, which
    /// only shows up on replies long enough to span several reports — so it
    /// looks like an intermittent parsing fault rather than an off-by-one.
    /// </summary>
    public static ReadOnlySpan<byte> PayloadOf(ReadOnlySpan<byte> report)
    {
        if (report.Length < 3 || report[0] != InReportId) return ReadOnlySpan<byte>.Empty;
        int length = report[1] | (report[2] << 8);
        length = Math.Min(length, report.Length - 3);
        return length <= 0 ? ReadOnlySpan<byte>.Empty : report.Slice(3, length);
    }

    internal static byte[] TagFor(bool takesArgument) => takesArgument ? SetTag : ReadTag;
}
