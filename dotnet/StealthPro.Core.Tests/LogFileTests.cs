namespace StealthPro.Core.Tests;

public sealed class LogFileTests : IDisposable
{
    private static readonly DateTime Noon = new(2026, 9, 25, 12, 0, 0);

    private readonly string _folder = Path.Combine(Path.GetTempPath(), "neap-log-" + Guid.NewGuid().ToString("N"));

    private string LogPath => Path.Combine(_folder, "app.log");

    private string[] Lines() => File.ReadAllLines(LogPath);

    public void Dispose()
    {
        if (Directory.Exists(_folder)) Directory.Delete(_folder, recursive: true);
    }

    [Fact]
    public void ALineIsStampedWithWhenItHappened()
    {
        Assert.True(new LogFile(LogPath).Write("started", Noon));
        Assert.Equal(["2026-09-25 12:00:00.0  started"], Lines());
    }

    [Fact]
    public void ALineWrittenLateGoesWhereItsTimeBelongs()
    {
        var log = new LogFile(LogPath);
        log.Write("second", Noon.AddSeconds(2));
        log.Write("first", Noon);
        Assert.Equal(["first", "second"], Lines().Select(l => l[23..]));
    }

    [Fact]
    public void OnlyTheMostRecentLinesAreKept()
    {
        var log = new LogFile(LogPath, keepLines: 2);
        for (int i = 0; i < 3; i++) log.Write($"line {i}", Noon.AddSeconds(i));
        Assert.Equal(["line 1", "line 2"], Lines().Select(l => l[23..]));
    }

    [Fact]
    public void ALineThatCannotBeWrittenIsKeptAndTheFailureSaid()
    {
        var log = new LogFile(LogPath);
        log.Write("before", Noon);
        using (new FileStream(LogPath, FileMode.Open, FileAccess.Read, FileShare.None))
            Assert.False(log.Write("while held", Noon.AddSeconds(1)));

        Assert.True(log.Write("after", Noon.AddSeconds(2)));
        var lines = Lines();
        Assert.Equal(4, lines.Length);
        Assert.Equal(["before", "while held"], lines.Take(2).Select(l => l[23..]));
        Assert.StartsWith("log: 2 line(s) from 2026-09-25 12:00:01.0 on were written late", lines[2][23..], StringComparison.Ordinal);
        Assert.Contains("IOException", lines[2], StringComparison.Ordinal);
        Assert.Equal("after", lines[3][23..]);
    }

    [Fact]
    public void TwoCopiesWritingTheSameFileKeepEachOthersLines()
    {
        var one = new LogFile(LogPath);
        var other = new LogFile(LogPath);
        Parallel.For(0, 40, i => (i % 2 == 0 ? one : other).Write($"line {i:00}", Noon.AddSeconds(i)));
        Assert.Equal(40, Lines().Length);
    }
}
