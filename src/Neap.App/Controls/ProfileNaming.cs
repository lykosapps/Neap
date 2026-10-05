using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;

namespace Neap.App.Controls;

/// <summary>The name dialog shared by saving a new profile and renaming one.</summary>
internal static class ProfileNaming
{
    /// <summary>
    /// Asks for a profile's name, checking live whether it is already taken.
    /// </summary>
    /// <param name="root">The dialog's XAML root, from whichever control is asking.</param>
    /// <param name="title">The dialog's title: naming a new profile or renaming one.</param>
    /// <param name="suggested">The name to start the field with, or empty for a new profile.</param>
    /// <param name="excludingId">
    /// The profile being renamed, whose own name is not a clash with itself;
    /// null while saving a new one.
    /// </param>
    /// <returns>The trimmed name, or an empty string if the dialog was cancelled.</returns>
    public static async Task<string> Ask(XamlRoot root, string title, string suggested, string? excludingId)
    {
        var field = new TextBox { Text = suggested, PlaceholderText = Strings.Get("Profile_NamePlaceholder"), MaxLength = 40 };
        // A placeholder alone is not a name a screen reader can rely on.
        AutomationProperties.SetName(field, Strings.Get("Profile_NamePlaceholder"));
        var note = new TextBlock
        {
            Style = (Style)Application.Current.Resources["SecondaryCaptionTextStyle"],
            TextWrapping = TextWrapping.Wrap,
        };
        var dialog = new NeapDialog
        {
            XamlRoot = root,
            Title = title,
            Content = new StackPanel { Spacing = 8, Children = { field, note } },
            PrimaryButtonText = Strings.Get("Dialog_Save"),
            CloseButtonText = Strings.Get("Dialog_Cancel"),
            DefaultButton = ContentDialogButton.Primary,
        };

        void Judge()
        {
            string typed = field.Text.Trim();
            bool taken = AppServices.Profiles.All.Any(p => p.Id != excludingId && Same(p.Name, typed));
            if (typed.Length == 0)
            {
                note.Text = Strings.Get("Profile_GiveName");
                dialog.IsPrimaryButtonEnabled = false;
            }
            else if (taken)
            {
                note.Text = Strings.Format("Profile_NameTaken", typed);
                dialog.IsPrimaryButtonEnabled = false;
            }
            else
            {
                note.Text = "";
                dialog.IsPrimaryButtonEnabled = true;
            }
        }

        field.TextChanged += (_, _) => Judge();
        Judge();
        field.SelectAll();

        var answer = await dialog.ShowAsync();
        return answer == ContentDialogResult.Primary ? field.Text.Trim() : "";
    }

    private static bool Same(string a, string b) =>
        string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);
}
