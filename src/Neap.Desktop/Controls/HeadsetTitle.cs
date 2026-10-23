using Neap.Core;

namespace Neap.Desktop.Controls;

/// <summary>The headset's name as a screen shows it.</summary>
internal static class HeadsetTitle
{
    /// <summary>The connected headset's label (see <see cref="HeadsetModels.Label"/>), and only a generic word for a headset Neap does not know.</summary>
    public static string Current() =>
        HeadsetModels.Label(AppServices.Headset.Model, AppServices.Headset.DeviceName) ?? Strings.Get("Headset_Model");
}
