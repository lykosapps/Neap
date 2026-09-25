using System.Text;
using Neap.Core.Protocol;

namespace Neap.Core.Tests.Protocol;

public class EventParserTests
{
    private static (List<DeviceEvent> Events, byte[] Remainder) Parse(string text) =>
        EventParser.Consume(Encoding.ASCII.GetBytes(text));

    [Fact]
    public void ReadsANotification()
    {
        var (events, remainder) = Parse("{\"UP\":\"SAF\",\"KVP\":{\"750\":\"1\"}}");

        var only = Assert.Single(events);
        Assert.Equal("UP", only.Kind);
        Assert.Equal("SAF", only.Category);
        Assert.Equal("1", only.Values["750"].GetString());
        Assert.Empty(remainder);
    }

    [Theory]
    [InlineData("3DT")]
    [InlineData("CG1")]
    [InlineData("TX4")]
    public void CategoriesWithDigitsAreKept(string category)
    {
        var (events, _) = Parse($"{{\"OR\":\"{category}\",\"KVP\":{{\"510\":\"50\"}}}}");

        Assert.Equal(category, Assert.Single(events).Category);
    }

    [Fact]
    public void NestedValuesAreNotCutShort()
    {
        var (events, _) = Parse(
            "{\"OR\":\"CG1\",\"KVP\":{\"1700\":{\"name\":\"De Mud\",\"bands\":[\"30\",\"-30\"]}}}");

        var slot = Assert.Single(events).Values["1700"];
        Assert.Equal("De Mud", slot.GetProperty("name").GetString());
        Assert.Equal(2, slot.GetProperty("bands").GetArrayLength());
    }

    [Fact]
    public void AReplySplitMidWordWaitsForTheRest()
    {
        const string reply =
            "{\"OR\":\"CG1\",\"KVP\":{\"1700\":{\"name\":\"De Mud\",\"bands\":[\"30\"]}}}";

        var first = Parse(reply[..33]);
        Assert.Empty(first.Events);

        var second = EventParser.Consume(
            first.Remainder.Concat(Encoding.ASCII.GetBytes(reply[33..])).ToArray());
        Assert.Equal("CG1", Assert.Single(second.Events).Category);
        Assert.Empty(second.Remainder);
    }

    [Fact]
    public void EventsBackToBackAreAllRead()
    {
        var (events, _) = Parse(
            "{\"UP\":\"GSI\",\"KVP\":{\"240\":\"89\"}}{\"UP\":\"3DT\",\"KVP\":{\"510\":\"40\"}}");

        Assert.Equal(new[] { "GSI", "3DT" }, events.Select(e => e.Category));
    }

    [Fact]
    public void WhatCannotBeDecodedIsRecordedNotDropped()
    {
        const string broken = "{\"OR\":\"GSI\",\"KVP\":{\"240\":89x}}";

        var (events, _) = Parse(broken);

        Assert.Empty(events);
        Assert.Contains(EventParser.Unrecognised, fragment => fragment.Contains("89x", StringComparison.Ordinal));
    }
}
