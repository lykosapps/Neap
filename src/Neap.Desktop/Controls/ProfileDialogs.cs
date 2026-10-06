using Avalonia.Controls;

namespace Neap.Desktop.Controls;

/// <summary>The dialogs shared by everything that saves a profile or has to say one failed.</summary>
internal static class ProfileDialogs
{
    /// <summary>Asks for a name and saves the headset's settings as a new profile.</summary>
    /// <remarks>
    /// Shared by the profile menu in the header and the Profiles page, so
    /// both save, name and fail the same way.
    /// </remarks>
    /// <param name="over">The control asking, whose window the dialog opens over.</param>
    public static async Task SaveCurrentAsNew(Control over)
    {
        string name = await ProfileNaming.Ask(over, Strings.Get("Profile_NewTitle"), "", excludingId: null);
        if (name.Length == 0) return;
        if (AppServices.Profiles.SaveNew(name) is { } trouble)
            await Complain(over, Strings.Get("Profile_CouldNotSaveTitle"), trouble);
    }

    /// <summary>Tells the person something went wrong, with one button to close it.</summary>
    public static async Task Complain(Control over, string title, string message) => await new NeapDialog
    {
        Heading = title,
        Body = new TextBlock { Text = message },
        CloseButtonText = Strings.Get("Dialog_OK"),
    }.ShowAsync(over);
}
