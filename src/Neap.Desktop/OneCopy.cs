using Neap.Core;

namespace Neap.Desktop;

/// <summary>Keeps the app to one copy at a time, by holding a lock on a file in its folder.</summary>
/// <remarks>
/// The lock goes when the process does, however it ends, so a copy that
/// crashed never leaves the next one locked out. A pretend run has its own
/// folder and so its own lock, and runs beside the real app.
/// </remarks>
public sealed class OneCopy : IDisposable
{
    private readonly FileStream _lock;

    private OneCopy(FileStream held) => _lock = held;

    /// <summary>Takes the lock, or returns null when another copy has it.</summary>
    public static OneCopy? Take()
    {
        Directory.CreateDirectory(AppFolder.Path);
        try
        {
            return new OneCopy(new FileStream(Path.Combine(AppFolder.Path, "running.lock"),
                FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None));
        }
        catch (IOException)
        {
            return null;
        }
    }

    public void Dispose() => _lock.Dispose();
}
