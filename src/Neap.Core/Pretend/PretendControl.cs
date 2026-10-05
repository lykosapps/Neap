using System.Globalization;
using System.IO.Pipes;
using System.Text;
using System.Text.Json.Nodes;
using Neap.Core.Audio;
using Neap.Core.Presets;
using Neap.Core.Settings;

namespace Neap.Core.Pretend;

/// <summary>
/// Lets a test script outside the app read what the pretend headset was
/// sent and make it report changes, over a named pipe.
/// </summary>
/// <remarks>
/// <para>
/// One command per line, one line of JSON back: <c>{"ok":true,"result":…}</c>
/// or <c>{"ok":false,"error":"…"}</c>. A key is a registry name or its hex
/// number, such as <c>anc_level</c> or <c>0x760</c>.
/// </para>
/// <code>
///   writes                  the writes accepted, in order
///   refusals                the frames refused, and why
///   sent                    how many frames have been sent
///   clear                   forget all three
///   value KEY               what the headset holds for a key
///   report KEY VALUE        the headset changes a value itself and says so
///   on | off                switch the headset on or off
///   spare PERCENT|empty     put a spare battery in the Charging Dock's slot, or take it out
///   presets game|mic        the custom presets' names, by slot
///   volume output|input     Windows' volume and mute for the headset
///   format output|input     the headset's format in Windows
///   mix                     the mix being applied, or null
///   programs                every program running, by process name
///   start NAME | stop NAME  a program starts or closes
///   registry                every confirmed setting, its limits and its options' labels
/// </code>
/// <para>
/// The pipe is open to the current user only.
/// </para>
/// </remarks>
public sealed class PretendControl : IDisposable
{
    public const string DefaultPipe = "Neap.Pretend";

    private readonly PretendHeadset _headset;
    private readonly PretendWindows _windows;
    private readonly string _pipe;
    private readonly Action<string> _log;
    private readonly CancellationTokenSource _stopping = new();
    private Task? _serving;

    /// <param name="headset">The pretend headset a script drives and reads.</param>
    /// <param name="windows">The pretend Windows audio a script drives and reads.</param>
    /// <param name="log">Where a failure in the pipe itself is reported.</param>
    /// <param name="pipe">The pipe's name.</param>
    public PretendControl(PretendHeadset headset, PretendWindows windows, Action<string> log,
        string pipe = DefaultPipe)
    {
        _headset = headset;
        _windows = windows;
        _log = log;
        _pipe = pipe;
    }

    /// <summary>Starts answering on the pipe, one script at a time.</summary>
    public void Start() => _serving ??= Task.Run(Serve);

    /// <remarks>
    /// The next script's end of the pipe is opened before the current script
    /// is served. On Linux a pipe is a socket, and one closed with nothing
    /// listening behind it resets a script that connects as the last one
    /// leaves; on Windows a script connecting then simply waits its turn.
    /// </remarks>
    private async Task Serve()
    {
        NamedPipeServerStream? next = null;
        try
        {
            while (!_stopping.IsCancellationRequested)
            {
                next ??= Listen();
                await using var server = next;
                await server.WaitForConnectionAsync(_stopping.Token).ConfigureAwait(false);
                next = Listen();
                await Answer(server).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) when (ex is UnauthorizedAccessException or InvalidOperationException or IOException)
        {
            _log($"pretend control: the pipe cannot be served, so no script can reach the headset: {ex.Message}");
        }
        finally
        {
            if (next is not null) await next.DisposeAsync().ConfigureAwait(false);
        }
    }

    private NamedPipeServerStream Listen() =>
        new(_pipe, PipeDirection.InOut, 2, PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);

    /// <summary>Answers one script until it leaves.</summary>
    private async Task Answer(NamedPipeServerStream server)
    {
        try
        {
            using var reader = new StreamReader(server);
            // Replies go straight to the pipe: a StreamWriter flushes when
            // disposed, and that flush fails once the script has closed its end.
            while (await reader.ReadLineAsync(_stopping.Token).ConfigureAwait(false) is string line)
                await server.WriteAsync(Encoding.UTF8.GetBytes(Handle(line) + "\n"), _stopping.Token).ConfigureAwait(false);
        }
        catch (IOException ex)
        {
            // A script that goes away mid-line is ordinary; the next one is
            // already waiting on a pipe of its own.
            _log($"pretend control: a script's connection ended: {ex.Message}");
        }
    }

    /// <summary>Carries out one command and returns its reply.</summary>
    public string Handle(string line)
    {
        string[] words = line.Split(' ', 3, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        try
        {
            JsonNode? result = (words.FirstOrDefault(), words.Length) switch
            {
                ("writes", 1) => new JsonArray(_headset.Writes
                    .Select(w => (JsonNode)new JsonObject { ["key"] = Hex(w.Key), ["value"] = w.Value }).ToArray()),
                ("refusals", 1) => new JsonArray(_headset.Refusals
                    .Select(r => (JsonNode)new JsonObject { ["frame"] = r.Frame, ["reason"] = r.Reason }).ToArray()),
                ("sent", 1) => _headset.Sent.Count,
                ("clear", 1) => Done(_headset.ClearRecord),
                ("value", 2) => _headset.Value(Key(words[1])),
                ("report", 3) => Done(() => _headset.Report(Key(words[1]), words[2])),
                ("on", 1) => Done(() => _headset.On = true),
                ("off", 1) => Done(() => _headset.On = false),
                ("spare", 2) => Done(() => _headset.Spare(ChargeOf(words[1]))),
                ("presets", 2) => new JsonArray(_headset.Customs(BankOf(words[1]))
                    .Select(p => (JsonNode?)p?.Name).ToArray()),
                ("volume", 2) => Volume(FlowOf(words[1])),
                ("format", 2) => _windows.Formats(FlowOf(words[1])).Current is { } f
                    ? $"{f.Bits}/{f.Rate.ToString(CultureInfo.InvariantCulture)}" : null,
                ("mix", 1) => _windows.Mix,
                ("programs", 1) => new JsonArray(_windows.Programs().Select(p => (JsonNode?)p).ToArray()),
                ("start", >= 2) => Done(() => _windows.Start(Rest(line))),
                ("stop", >= 2) => Done(() => _windows.Stop(Rest(line))),
                ("registry", 1) => new JsonArray(Registry.All.Select(Describe).ToArray()),
                _ => throw new ArgumentException($"not a command: '{line}'"),
            };
            return new JsonObject { ["ok"] = true, ["result"] = result }.ToJsonString();
        }
        catch (Exception ex) when (ex is ArgumentException or KeyNotFoundException)
        {
            return new JsonObject { ["ok"] = false, ["error"] = ex.Message }.ToJsonString();
        }
    }

    private static JsonNode Done(Action action)
    {
        action();
        return "ok";
    }

    private JsonNode Volume(Flow flow)
    {
        var info = _windows.Describe(flow);
        return new JsonObject { ["percent"] = info.Percent, ["muted"] = info.Muted };
    }

    private static JsonNode Describe(SettingKey key) => new JsonObject
    {
        ["key"] = Hex(key.Key),
        ["name"] = key.Name,
        ["kind"] = key.Kind.ToString(),
        ["writable"] = key.Writable,
        ["min"] = key.Minimum,
        ["max"] = key.Maximum,
        ["options"] = key.Options is null ? null
            : new JsonObject(key.Options.OrderBy(o => o.Key).Select(o =>
                KeyValuePair.Create(o.Key.ToString(CultureInfo.InvariantCulture), (JsonNode?)o.Value))),
    };

    private static int Key(string text) => Registry.Resolve(text).Key;

    /// <summary>Everything after the command, since a program's name can have spaces in it.</summary>
    private static string Rest(string line) => line.Trim().Split(' ', 2)[1].Trim();

    private static int? ChargeOf(string text) =>
        text == "empty" ? null
        : int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out int percent) ? percent
        : throw new ArgumentException($"a spare battery is a percentage or empty, not '{text}'");

    private static Bank BankOf(string text) => text switch
    {
        "game" => Bank.Game,
        "mic" => Bank.Mic,
        _ => throw new ArgumentException($"a bank is game or mic, not '{text}'"),
    };

    private static Flow FlowOf(string text) => text switch
    {
        "output" => Flow.Output,
        "input" => Flow.Input,
        _ => throw new ArgumentException($"a device is output or input, not '{text}'"),
    };

    private static string Hex(int key) => "0x" + key.ToString("x", CultureInfo.InvariantCulture);

    public void Dispose()
    {
        _stopping.Cancel();
        try { _serving?.Wait(TimeSpan.FromSeconds(2)); }
        catch (AggregateException) { }
        _stopping.Dispose();
    }
}
