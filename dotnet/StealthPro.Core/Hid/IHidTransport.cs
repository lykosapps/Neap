namespace StealthPro.Core.Hid;

/// <summary>
/// The one report pair the headset is reached through. <see cref="HidTransport"/>
/// is the real device; tests stand in for it with recorded replies.
/// </summary>
public interface IHidTransport : IDisposable
{
    /// <summary>Which Turtle Beach device this is.</summary>
    ushort ProductId { get; }

    string Describe();

    /// <summary>Write one output report, report id included.</summary>
    void SendOutput(ReadOnlySpan<byte> report);

    /// <summary>Read one input report, report id included.</summary>
    byte[] GetInput(byte reportId = HidTransport.InReportId);
}
