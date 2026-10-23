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
///   reads                   how many times each group of settings has been read
///   clear                   forget all four
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
///   recording               every program recording from a microphone
///   record NAME | unrecord NAME  a program starts or stops recording
///   registry                every confirmed setting, its limits and its options' labels
///   focus first|next|previous  put the keyboard focus on the window's first control, or move it
///                           as Tab or Shift+Tab would, and say what took it
///   page NAME               open a page from the menu, such as audio
///   mini                    open the mini panel, as a click on the icon by the clock does
///   press NAME              press the control or menu item a screen reader calls NAME, in any of the app's windows
/// </code>
/// <para>
/// The pipe is open to the current user only.
/// </para>
/// </remarks>
/// <summary>Where a test script asks the keyboard focus to go: the window's first control, or on or back as Tab and Shift+Tab take it.</summary>
public enum FocusMove { First, Next, Previous }

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

    /// <summary>Gets or sets what moves the keyboard focus in the app's window (first, next or previous) and describes what took it, or null when nothing did.</summary>
    /// <remarks>
    /// Set by the app once its window is open. A script that presses Tab
    /// needs the window in front and the keyboard, so it cannot run while
    /// somebody uses the machine; this moves focus the way the key does,
    /// with the window left where it is.
    /// </remarks>
    public Func<FocusMove, JsonNode?>? MoveFocus { get; set; }

    /// <summary>Gets or sets what opens a page of the app's window by its name, such as audio, saying whether there is one.</summary>
    /// <remarks>
    /// A page chosen through UI Automation brings the window to the front, as
    /// most of its actions do; this opens one with the window left where it is.
    /// </remarks>
    public Func<string, bool>? OpenPage { get; set; }

    /// <summary>Gets or sets what opens the mini panel, as a click on the icon by the clock does.</summary>
    /// <remarks>
    /// The icon is the system's, out of the reach of UI Automation on Windows
    /// 11 short of walking the notification area with the keyboard.
    /// </remarks>
    public Action? OpenMini { get; set; }

    /// <summary>Gets or sets what presses the control or menu item a screen reader calls by a name, saying whether there is one.</summary>
    /// <remarks>
    /// Some menu items answer only Enter, which a script on a desktop of its
    /// own cannot send: a key goes to the person's desktop. This presses them
    /// inside the app instead.
    /// </remarks>
    public Func<string, bool>? Press { get; set; }

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
                ("reads", 1) => new JsonObject(_headset.Reads.Select(r => KeyValuePair.Create(r.Key, (JsonNode?)r.Value))),
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
                ("recording", 1) => new JsonArray(_windows.Recording().Select(p => (JsonNode?)p).ToArray()),
                ("record", >= 2) => Done(() => _windows.Record(Rest(line))),
                ("unrecord", >= 2) => Done(() => _windows.StopRecording(Rest(line))),
                ("registry", 1) => new JsonArray(Registry.All.Select(Describe).ToArray()),
                ("page", 2) => (OpenPage ?? throw new ArgumentException("there is no window to open a page in"))(words[1])
                    ? "ok" : throw new ArgumentException($"no page called '{words[1]}'"),
                ("focus", 2) => (MoveFocus ?? throw new ArgumentException("there is no window to move focus in"))(MoveOf(words[1])),
                ("mini", 1) => Done(OpenMini ?? throw new ArgumentException("there is no window to open the mini panel from")),
                ("press", >= 2) => (Press ?? throw new ArgumentException("there is no window to press in"))(Rest(line))
                    ? "ok" : throw new ArgumentException($"nothing called '{Rest(line)}' to press"),
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

    private static FocusMove MoveOf(string text) => text switch
    {
        "first" => FocusMove.First,
        "next" => FocusMove.Next,
        "previous" => FocusMove.Previous,
        _ => throw new ArgumentException($"focus goes first, next or previous, not '{text}'"),
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
