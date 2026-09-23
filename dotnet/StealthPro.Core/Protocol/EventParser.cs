using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace StealthPro.Core.Protocol;

/// <summary>One decoded reply or notification.</summary>
/// <param name="Kind">"UP" (unsolicited update) or "OR" (operation response).</param>
/// <param name="Category">GSI, BT, SAF, Mic, Btn, Enc, AQG, CG1, TX1 …</param>
/// <param name="Values">key -&gt; value; keys are lowercase hex without "0x".</param>
public readonly record struct DeviceEvent(
    string Kind, string Category, IReadOnlyDictionary<string, JsonElement> Values)
{
    public override string ToString() =>
        $"{Kind}/{Category}: " + string.Join(", ",
            Values.Select(p => $"{p.Key}={Render(p.Value)}"));

    public static string Render(JsonElement value) =>
        value.ValueKind == JsonValueKind.String ? value.GetString() ?? "" : value.GetRawText();
}

/// <summary>
/// Extracts device events from the headset's byte stream.
/// </summary>
/// <remarks>
/// <para>
/// Category names can contain digits. "3DT" carries the game/chat mix and
/// "CG1".."CG5" carry the custom equaliser slots; a letters-only pattern
/// drops every one of them silently, which looks like the device sending
/// nothing.
/// </para>
/// <para>
/// Where an event ends is found by counting braces, not by pattern matching.
/// A value can itself be an object: the preset slots answer with
/// <c>{"1700":{"name":…,"bands":[…]}}</c>. A pattern that stops at the first
/// closing brace cuts those in half, the JSON fails, and the reply looks
/// unanswered.
/// </para>
/// <para>
/// Anything event-shaped that does not decode is recorded in
/// <see cref="Unrecognised"/> rather than discarded, so that "cannot parse"
/// is never mistaken for "does not exist".
/// </para>
/// </remarks>
public static class EventParser
{
    private static readonly Regex EventStart =
        new(@"\{""(UP|OR)"":""([A-Za-z0-9_]+)"",""KVP"":", RegexOptions.Compiled);

    private static readonly Regex Loose =
        new(@"\{""[^{}]{0,40}""\s*:.{0,400}?\}\}", RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly List<string> UnrecognisedFragments = new();
    private static readonly object UnrecognisedGate = new();

    /// <summary>
    /// The most recent event-shaped fragments that did not decode, up to 50.
    /// Returns a copy, so it can be read while another thread is parsing.
    /// </summary>
    public static IReadOnlyList<string> Unrecognised
    {
        get { lock (UnrecognisedGate) return UnrecognisedFragments.ToArray(); }
    }

    /// <summary>
    /// Parses the complete events in a buffer and returns them with whatever
    /// was not consumed.
    /// </summary>
    /// <remarks>
    /// Replies longer than one report continue in follow-on reports, and the
    /// boundary can fall anywhere, including mid-string, so the caller must
    /// keep appending to the remainder rather than clearing it. An unrelated
    /// notification can arrive part-way through a long reply.
    /// </remarks>
    public static (List<DeviceEvent> Events, byte[] Remainder) Consume(ReadOnlySpan<byte> buffer)
    {
        var events = new List<DeviceEvent>();
        var spans = new List<(int Start, int End)>();
        int consumed = 0;

        // The frames are ASCII where it matters; Latin-1 keeps byte offsets
        // and character offsets identical, which the brace counting relies on.
        string text = Encoding.Latin1.GetString(buffer);

        foreach (Match match in EventStart.Matches(text))
        {
            int kvpStart = match.Index + match.Length;
            if (kvpStart >= text.Length || text[kvpStart] != '{') continue;

            int kvpEnd = ObjectEnd(text, kvpStart);
            if (kvpEnd < 0) break;                       // truncated; wait for the rest
            // The event object closes one byte after its KVP does. If that
            // byte has not arrived, the reply is still in flight.
            if (kvpEnd >= text.Length || text[kvpEnd] != '}') break;

            int end = kvpEnd + 1;
            Dictionary<string, JsonElement>? values;
            try
            {
                values = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(
                    text[kvpStart..kvpEnd]);
            }
            catch (JsonException)
            {
                continue;
            }
            if (values is null) continue;

            events.Add(new DeviceEvent(match.Groups[1].Value, match.Groups[2].Value, values));
            spans.Add((match.Index, end));
            consumed = end;
        }

        NoteUnrecognised(text, spans);
        return (events, buffer[consumed..].ToArray());
    }

    /// <summary>Index just past the object beginning at <paramref name="start"/>, or -1.</summary>
    private static int ObjectEnd(string text, int start)
    {
        int depth = 0;
        bool inString = false, escaped = false;
        for (int i = start; i < text.Length; i++)
        {
            char c = text[i];
            if (inString)
            {
                if (escaped) escaped = false;
                else if (c == '\\') escaped = true;
                else if (c == '"') inString = false;
                continue;
            }
            if (c == '"') inString = true;
            else if (c == '{') depth++;
            else if (c == '}' && --depth == 0) return i + 1;
        }
        return -1;
    }

    /// <summary>Records anything event-shaped that did not become an event.</summary>
    /// <remarks>
    /// Only spans that produced an event count as covered. A span that matched
    /// the start pattern but failed to decode is the case worth reporting.
    /// </remarks>
    private static void NoteUnrecognised(string text, List<(int Start, int End)> parsed)
    {
        foreach (Match loose in Loose.Matches(text))
        {
            int start = loose.Index, end = loose.Index + loose.Length;
            if (parsed.Any(s => s.Start <= start && end <= s.End)) continue;
            string fragment = loose.Value.Length > 200 ? loose.Value[..200] : loose.Value;
            lock (UnrecognisedGate)
            {
                if (UnrecognisedFragments.Contains(fragment)) continue;
                UnrecognisedFragments.Add(fragment);
                if (UnrecognisedFragments.Count > 50) UnrecognisedFragments.RemoveAt(0);
            }
        }
    }
}
