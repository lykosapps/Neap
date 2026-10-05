namespace Neap.Core.Hid;

/// <summary>
/// The output and input report pair the headset is reached through.
/// </summary>
/// <remarks>
/// <see cref="SystemDevices"/> opens the real device; tests substitute
/// recorded replies.
/// </remarks>
public interface IHidTransport : IDisposable
{
    /// <summary>The USB product ID of the Turtle Beach device.</summary>
    ushort ProductId { get; }

    string Describe();

    /// <summary>Writes one output report, report id included.</summary>
    void SendOutput(ReadOnlySpan<byte> report);

    /// <summary>Reads one input report, report id included.</summary>
    byte[] GetInput(byte reportId = HidControl.InReportId);
}
