using Neap.Core.Updates;

namespace Neap.Core.Tests.Updates;

public sealed class FolderSwapTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "neap-swap-" + Guid.NewGuid().ToString("N"));

    private string Target => Path.Combine(_root, "Neap");
    private string Incoming => Path.Combine(_root, "new");
    private string Replaced => Path.Combine(_root, "old");

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private static void Put(string folder, string relative, string text)
    {
        string path = Path.Combine(folder, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
    }

    private string Read(string relative) => File.ReadAllText(Path.Combine(Target, relative));

    private void Swap() => FolderSwap.Swap(Target, Incoming, Replaced);

    [Fact]
    public void TheNewVersionsFilesTakeTheOldOnesPlaces()
    {
        Put(Target, "Neap.exe", "old");
        Put(Target, @"en-US\Neap.resources.dll", "old");
        Put(Incoming, "Neap.exe", "new");
        Put(Incoming, @"en-US\Neap.resources.dll", "new");

        Swap();

        Assert.Equal("new", Read("Neap.exe"));
        Assert.Equal("new", Read(@"en-US\Neap.resources.dll"));
        Assert.Equal("old", File.ReadAllText(Path.Combine(Replaced, "Neap.exe")));
    }

    [Fact]
    public void FilesOnlyTheNewVersionHasAreAdded()
    {
        Put(Target, "Neap.exe", "old");
        Put(Incoming, "Neap.exe", "new");
        Put(Incoming, @"Assets\logo.png", "new");

        Swap();

        Assert.Equal("new", Read(@"Assets\logo.png"));
    }

    [Fact]
    public void FilesTheNewVersionDoesNotHaveAreLeftAlone()
    {
        Put(Target, "Neap.exe", "old");
        Put(Target, "notes.txt", "someone's");
        Put(Incoming, "Neap.exe", "new");

        Swap();

        Assert.Equal("someone's", Read("notes.txt"));
    }

    [Fact]
    public void AFileThatCannotBeMovedLeavesTheFolderAsItWas()
    {
        // Only Windows refuses to rename a file another program has open.
        Assert.SkipUnless(OperatingSystem.IsWindows(), "this system lets a file in use be renamed");

        Put(Target, "a.dll", "old");
        Put(Target, "b.dll", "old");
        Put(Target, "c.dll", "old");
        Put(Incoming, "a.dll", "new");
        Put(Incoming, "b.dll", "new");
        Put(Incoming, "c.dll", "new");

        // Held open without letting it be renamed, as a program that has
        // the file open can.
        using (new FileStream(Path.Combine(Target, "b.dll"), FileMode.Open, FileAccess.Read, FileShare.Read))
            Assert.ThrowsAny<IOException>(Swap);

        Assert.Equal("old", Read("a.dll"));
        Assert.Equal("old", Read("b.dll"));
        Assert.Equal("old", Read("c.dll"));
        Assert.Equal(3, Directory.GetFiles(Incoming).Length);
    }
}
