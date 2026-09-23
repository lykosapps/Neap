using System.IO.Pipes;
using System.Text.Json;
using StealthPro.Core.Pretend;

namespace StealthPro.Core.Tests.Pretend;

public class PretendControlTests
{
    private static JsonElement Reply(PretendControl control, string line) =>
        JsonDocument.Parse(control.Handle(line)).RootElement;

    private static (PretendHeadset, PretendControl) Make()
    {
        var headset = new PretendHeadset();
        return (headset, new PretendControl(headset, new PretendWindows(), _ => { }, "unused"));
    }

    [Fact]
    public void ListsTheWritesItAccepted()
    {
        var (headset, control) = Make();
        using var client = new HeadsetClient(allowWrites: true, transport: headset.Open());
        client.Set(0x760, 55);

        var writes = Reply(control, "writes").GetProperty("result");

        Assert.Equal("0x760", writes[0].GetProperty("key").GetString());
        Assert.Equal("55", writes[0].GetProperty("value").GetString());
    }

    [Fact]
    public void AReportedChangeReachesTheHeadsetByNameOrNumber()
    {
        var (headset, control) = Make();

        Assert.True(Reply(control, "report anc 0").GetProperty("ok").GetBoolean());
        Assert.True(Reply(control, "report 0x760 20").GetProperty("ok").GetBoolean());

        Assert.Equal(("0", "20"), (headset.Value(0x750), headset.Value(0x760)));
    }

    [Theory]
    [InlineData("report anc 2")]        // not a toggle's value
    [InlineData("report nothing 1")]    // not a setting
    [InlineData("value")]               // no key
    [InlineData("presets both")]
    [InlineData("jump")]
    public void AnythingItCannotDoIsAnErrorNotASilence(string line)
    {
        var (_, control) = Make();

        var reply = Reply(control, line);

        Assert.False(reply.GetProperty("ok").GetBoolean());
        Assert.NotEmpty(reply.GetProperty("error").GetString()!);
    }

    [Fact]
    public void TheRegistryCarriesEachSettingsLimits()
    {
        var (_, control) = Make();

        var anc = Reply(control, "registry").GetProperty("result").EnumerateArray()
            .Single(k => k.GetProperty("name").GetString() == "anc_level");

        Assert.Equal((0, 100, true),
            (anc.GetProperty("min").GetInt32(), anc.GetProperty("max").GetInt32(), anc.GetProperty("writable").GetBoolean()));
    }

    [Fact]
    public void TheRegistryNamesEachOption()
    {
        var (_, control) = Make();

        var dial = Reply(control, "registry").GetProperty("result").EnumerateArray()
            .Single(k => k.GetProperty("name").GetString() == "dial_function");

        Assert.Equal("Game and chat mix", dial.GetProperty("options").GetProperty("2").GetString());
    }

    [Fact]
    public async Task AnswersOverThePipe()
    {
        string pipe = $"Neap.Pretend.Test.{Guid.NewGuid():N}";
        var headset = new PretendHeadset();
        using var control = new PretendControl(headset, new PretendWindows(), _ => { }, pipe);
        control.Start();

        await using var client = new NamedPipeClientStream(".", pipe, PipeDirection.InOut);
        await client.ConnectAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        using var reader = new StreamReader(client);
        await using var writer = new StreamWriter(client) { AutoFlush = true };
        await writer.WriteLineAsync("value battery");

        string? reply = await reader.ReadLineAsync(TestContext.Current.CancellationToken);

        Assert.Equal("{\"ok\":true,\"result\":\"76\"}", reply);
    }
}
