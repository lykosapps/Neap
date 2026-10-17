using System.Globalization;
using System.Text.Json;
using Neap.Core.Settings;

namespace Neap.Desktop.Controls;

/// <summary>The headset's name as a screen shows it.</summary>
internal static class HeadsetTitle
{
    /// <summary>The name its owner gave the headset, else the connected model's own name, and only a generic word for a headset Neap does not know.</summary>
    public static string Current()
    {
        var headset = AppServices.Headset;
        headset.Values.TryGetValue(HeadsetLabel.Key.ToString("x", CultureInfo.InvariantCulture), out var given);
        return HeadsetLabel.Given(given.ValueKind == JsonValueKind.Undefined ? null : given.ToString())
            ?? headset.Model?.Name ?? Strings.Get("Headset_Model");
    }
}
