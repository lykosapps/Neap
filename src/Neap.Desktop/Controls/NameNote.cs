using Avalonia.Automation;
using Avalonia.Controls;

namespace Neap.Desktop.Controls;

/// <summary>The note under a name field in a dialog, which says why the name cannot be saved.</summary>
internal static class NameNote
{
    /// <summary>Makes the note for a field: a live region whose words are also the field's help text.</summary>
    /// <remarks>
    /// A screen reader then says why Save has turned off as the reason
    /// appears, and again on the field, rather than leaving the person to
    /// find Save off with no reason.
    /// </remarks>
    public static TextBlock For(TextBox field)
    {
        var note = new TextBlock { Classes = { "caption", "secondary" } };
        AutomationProperties.SetLiveSetting(note, AutomationLiveSetting.Polite);
        note.PropertyChanged += (_, e) =>
        {
            if (e.Property == TextBlock.TextProperty) AutomationProperties.SetHelpText(field, note.Text);
        };
        return note;
    }
}
