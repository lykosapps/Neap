using System.IO.Pipes;
using Neap.Core;

namespace Neap.Desktop;

/// <summary>Keeps the app to one copy at a time, by holding a lock on a file in its folder.</summary>
/// <remarks>
/// <para>
/// The lock goes when the process does, however it ends, so a copy that
/// crashed never leaves the next one locked out. A pretend run has its own
/// folder and so its own lock, and runs beside the real app.
/// </para>
/// <para>
/// A second launch asks the running copy to show its window and quits: someone
/// opening the app while it sits in the notification area wants the window,
/// not a second copy. The ask goes down a named pipe that only the person's
/// own account can open, and that each side checks the other end of belongs to
/// that account.
/// </para>
/// </remarks>
public sealed class OneCopy : IDisposable
{
    private readonly FileStream _lock;
    private readonly CancellationTokenSource _stopping = new();
    private bool _disposed;

    private OneCopy(FileStream held) => _lock = held;

    /// <summary>The copy this process holds, once it has taken the lock.</summary>
    public static OneCopy? Current { get; private set; }

    /// <summary>One per person: on a system two people are signed in to, a name they shared would hand one person's copy the other's request.</summary>
    private static string PipeName => (Pretend.Active ? "Neap.Pretend.Show." : "Neap.Show.") + Environment.UserName;

    /// <summary>Takes the lock, or returns null when another copy has it.</summary>
    public static OneCopy? Take()
    {
        Directory.CreateDirectory(AppFolder.Path);
        try
        {
            return Current = new OneCopy(new FileStream(Path.Combine(AppFolder.Path, "running.lock"),
                FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None));
        }
        catch (IOException)
        {
            return null;
        }
    }

    /// <summary>Asks the copy that is running to show its window.</summary>
    public static void AskToShow()
    {
        try
        {
            using var pipe = new NamedPipeClientStream(".", PipeName, PipeDirection.Out, PipeOptions.CurrentUserOnly);
            pipe.Connect(1000);
        }
        catch (Exception ex) when (ex is TimeoutException or IOException or UnauthorizedAccessException)
        {
            AppLog.Write($"could not show the running copy: {ex.Message}");
        }
    }

    /// <summary>Calls <paramref name="show"/>, on a thread of its own, each time another launch asks for the window.</summary>
    /// <remarks>
    /// A pipe that cannot be opened, because something else holds its name,
    /// is tried again every few seconds rather than at once, and logged only
    /// the first time, so a lasting clash costs neither the processor nor
    /// the log.
    /// </remarks>
    public void ListenForShow(Action show) => _ = Task.Run(async () =>
    {
        bool refused = false;
        while (!_stopping.IsCancellationRequested)
        {
            NamedPipeServerStream pipe;
            try
            {
                pipe = new NamedPipeServerStream(PipeName, PipeDirection.In, 1,
                    PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                refused = false;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                if (!refused) AppLog.Write($"a second launch cannot show this window for now: {ex.Message}");
                refused = true;
                try { await Task.Delay(TimeSpan.FromSeconds(5), _stopping.Token); }
                catch (Exception stop) when (stop is OperationCanceledException or ObjectDisposedException) { return; }
                continue;
            }

            await using (pipe)
            {
                try
                {
                    await pipe.WaitForConnectionAsync(_stopping.Token);
                    show();
                }
                catch (Exception ex) when (ex is OperationCanceledException or ObjectDisposedException) { return; }
                catch (IOException ex)
                {
                    // Another launch hung up before it was answered; the next one still gets through.
                    AppLog.Write($"the show request was interrupted: {ex.Message}");
                }
            }
        }
    });

    /// <summary>Lets go of the lock and stops listening.</summary>
    /// <remarks>
    /// Safe to call twice: an update lets go of the lock before it starts the
    /// new version, and the process lets go of it again as it ends.
    /// </remarks>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (Current == this) Current = null;
        _stopping.Cancel();
        _lock.Dispose();
        _stopping.Dispose();
    }
}
