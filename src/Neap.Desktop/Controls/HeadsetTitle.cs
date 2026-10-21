using System.Globalization;
using System.Text.Json;
using Neap.Core;
using Neap.Core.Settings;

namespace Neap.Desktop.Controls;

/// <summary>The headset's name as a screen shows it.</summary>
internal static class HeadsetTitle
{
    /// <summary>The connected headset's label (see <see cref="HeadsetModels.Label"/>), and only a generic word for a headset Neap does not know.</summary>
    public static string Current()
    {
        var headset = AppServices.Headset;
        headset.Values.TryGetValue(HeadsetLabel.Key.ToString("x", CultureInfo.InvariantCulture), out var given);
        return HeadsetModels.Label(headset.Model, given.ValueKind == JsonValueKind.Undefined ? null : given.ToString())
            ?? Strings.Get("Headset_Model");
    }
}
