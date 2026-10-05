using Avalonia;
using Avalonia.Automation;

namespace Neap.Desktop.Localization;

/// <summary>Gives a control the words the resource file holds under a name, as <c>x:Uid</c> does in the Windows app.</summary>
/// <remarks>
/// <para>
/// Setting <c>loc:Uid.Value</c> to Mix_Title sets every property the file has an
/// entry for: <c>Mix_Title.Text</c> becomes the control's Text, and an
/// <c>AutomationProperties.Name</c> entry becomes the name a screen reader
/// reads. So a control's sentence, its name and its description are written
/// once, in one place, whatever the screen it is on.
/// </para>
/// <para>
/// An entry for a property the control does not have throws, at the moment
/// the screen is built: a sentence that never appears is a fault to find
/// at once.
/// </para>
/// </remarks>
public static class Uid
{
    /// <summary>The name of the file's entries this control takes its text from.</summary>
    public static readonly AttachedProperty<string?> ValueProperty =
        AvaloniaProperty.RegisterAttached<AvaloniaObject, string?>("Value", typeof(Uid));

    /// <summary>What the Windows app's file calls the automation name, which the file cannot spell shorter.</summary>
    private const string AutomationName = "[using:Microsoft.UI.Xaml.Automation]AutomationProperties.Name";

    static Uid() => ValueProperty.Changed.AddClassHandler<AvaloniaObject>((target, change) => Apply(target, change.NewValue as string));

    public static string? GetValue(AvaloniaObject element) => element.GetValue(ValueProperty);

    public static void SetValue(AvaloniaObject element, string? value) => element.SetValue(ValueProperty, value);

    private static void Apply(AvaloniaObject target, string? uid)
    {
        if (uid is null) return;
        foreach (var (name, text) in ResourceStrings.Of(uid))
        {
            // The access key the Windows app sets is a Windows convention,
            // and carries no text of its own.
            if (name == "AccessKey") continue;
            if (name == AutomationName)
            {
                AutomationProperties.SetName((Avalonia.Controls.Control)target, text);
                continue;
            }

            var property = AvaloniaPropertyRegistry.Instance.FindRegistered(target, name)
                ?? throw new InvalidOperationException($"{target.GetType().Name} has no {name}, which '{uid}' sets");
            target.SetValue(property, text);
        }
    }
}
