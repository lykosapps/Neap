using System.Text;
using StealthPro.Core.Hid;
using StealthPro.Core.Protocol;

namespace StealthPro.Core.Tests;

/// <summary>
/// A headset that answers from a script. Each read verb sent gets whatever
/// replies <see cref="Answers"/> holds for it, cut into reports the way the
/// real device cuts them.
/// </summary>
internal sealed class FakeTransport : IHidTransport
{
    private const int PayloadPerReport = 59;
    private readonly Queue<byte[]> _incoming = new();

    public Dictionary<string, string[]> Answers { get; } = new();
    public List<byte[]> Sent { get; } = new();

    public ushort ProductId => 0x229B;

    public string Describe() => "fake";

    /// <summary>The set_kvp arguments sent so far, in order.</summary>
    public IEnumerable<string> Writes =>
        Sent.Select(Encoding.ASCII.GetString)
            .Where(frame => frame.Contains("set_kvp", StringComparison.Ordinal))
            .Select(frame => frame[frame.IndexOf('{', StringComparison.Ordinal)..]);

    public void SendOutput(ReadOnlySpan<byte> report)
    {
        Sent.Add(report.ToArray());
        string verb = Encoding.ASCII.GetString(report[23..]);
        if (Answers.TryGetValue(verb, out var replies))
            foreach (string reply in replies) Push(reply);
    }

    /// <summary>Queue a reply or notification, split across reports.</summary>
    public void Push(string json)
    {
        byte[] bytes = Encoding.ASCII.GetBytes(json);
        for (int at = 0; at < bytes.Length; at += PayloadPerReport)
        {
            var chunk = bytes.AsSpan(at, Math.Min(PayloadPerReport, bytes.Length - at));
            var report = new byte[Frames.ReportLength];
            report[0] = Frames.InReportId;
            report[1] = (byte)chunk.Length;
            chunk.CopyTo(report.AsSpan(3));
            _incoming.Enqueue(report);
        }
    }

    public byte[] GetInput(byte reportId = HidTransport.InReportId) =>
        _incoming.Count > 0 ? _incoming.Dequeue() : [reportId, 0, 0];

    public void Dispose() { }
}
