namespace Neap.Core.Tests;

public sealed class WholeFileTests : IDisposable
{
    private readonly string _folder = Directory.CreateTempSubdirectory("whole-file-").FullName;

    private string Target => Path.Combine(_folder, "settings.json");

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    [Fact]
    public void ANewFileHoldsTheText()
    {
        WholeFile.Write(Target, "new");

        Assert.Equal("new", File.ReadAllText(Target));
    }

    [Fact]
    public void AnOldFileIsReplacedAndNothingIsLeftBeside()
    {
        File.WriteAllText(Target, "a much longer old text");

        WholeFile.Write(Target, "new");

        Assert.Equal("new", File.ReadAllText(Target));
        Assert.Equal([Target], Directory.GetFiles(_folder));
    }

    [Fact]
    public void AWriteLeftHalfDoneByACrashIsWrittenOver()
    {
        File.WriteAllText(Target + ".tmp", "half");

        WholeFile.Write(Target, "new");

        Assert.Equal("new", File.ReadAllText(Target));
        Assert.Equal([Target], Directory.GetFiles(_folder));
    }

    [Fact]
    public void AWriteThatFailsLeavesTheOldFileAsItWas()
    {
        // Only Windows refuses to rename over a file another program has open.
        Assert.SkipUnless(OperatingSystem.IsWindows(), "this system lets a file in use be renamed over");
        File.WriteAllText(Target, "old");

        using (new FileStream(Target, FileMode.Open, FileAccess.Read, FileShare.Read))
            Assert.Throws<UnauthorizedAccessException>(() => WholeFile.Write(Target, "new"));

        Assert.Equal("old", File.ReadAllText(Target));
        Assert.Equal([Target], Directory.GetFiles(_folder));
    }
}
