using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;

namespace Neap.Desktop.Controls;

/// <summary>Which button answered a <see cref="NeapDialog"/>.</summary>
public enum DialogAnswer
{
    /// <summary>The close button, Escape, or the window's own close.</summary>
    None,

    /// <summary>The primary button.</summary>
    Primary,

    /// <summary>The secondary button.</summary>
    Secondary,
}

/// <summary>A dialog in Neap's colours, with a primary button, an optional secondary one and a close button.</summary>
/// <remarks>
/// <para>
/// It opens over the window that asked for it and holds that window until it
/// is answered. Enter takes the primary button when it can be used, and
/// Escape closes it.
/// </para>
/// <para>
/// A dialog made only to say something has no primary button, and only the
/// close button.
/// </para>
/// </remarks>
public sealed class NeapDialog : Window
{
    // Shown only once it has words: a dialog with just a close button has no empty one beside it.
    private readonly Button _primary = new() { IsDefault = true, Classes = { "dialogbutton", "accent" }, IsVisible = false };
    private readonly Button _secondary = new() { Classes = { "dialogbutton" }, IsVisible = false };
    private readonly Button _close = new() { IsCancel = true, Classes = { "dialogbutton" } };
    private readonly ContentControl _body = new();
    private readonly TextBlock _heading = new() { Classes = { "title" } };

    public NeapDialog()
    {
        Width = 440;
        SizeToContent = SizeToContent.Height;
        CanResize = false;
        ShowInTaskbar = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Classes.Add("neapground");

        var buttons = new StackPanel { Classes = { "dialogbuttons" }, Children = { _primary, _secondary, _close } };
        Content = new StackPanel { Classes = { "dialog" }, Children = { _heading, _body, buttons } };

        // Fixed names for the screen checks, which find the buttons by them.
        AutomationProperties.SetAutomationId(_primary, "PrimaryButton");
        AutomationProperties.SetAutomationId(_secondary, "SecondaryButton");
        AutomationProperties.SetAutomationId(_close, "CloseButton");

        _close.Click += (_, _) => Close(DialogAnswer.None);
        _primary.Click += (_, _) => Close(DialogAnswer.Primary);
        _secondary.Click += (_, _) => Close(DialogAnswer.Secondary);
        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape) Close(DialogAnswer.None);
        };
    }

    /// <summary>What the dialog is about, as its window's title and as a heading over its body.</summary>
    public string? Heading
    {
        get => _heading.Text;
        set
        {
            _heading.Text = value;
            Title = value;
        }
    }

    /// <summary>What the dialog says or asks, shown under its title.</summary>
    public object? Body
    {
        get => _body.Content;
        set => _body.Content = value;
    }

    /// <summary>The words on the primary button; none for a dialog with only a close button.</summary>
    public string? PrimaryButtonText
    {
        get => _primary.IsVisible ? _primary.Content as string : null;
        set
        {
            _primary.Content = value;
            _primary.IsVisible = !string.IsNullOrEmpty(value);
        }
    }

    /// <summary>The words on the secondary button; none for a dialog without one.</summary>
    public string? SecondaryButtonText
    {
        get => _secondary.IsVisible ? _secondary.Content as string : null;
        set
        {
            _secondary.Content = value;
            _secondary.IsVisible = !string.IsNullOrEmpty(value);
        }
    }

    /// <summary>The words on the close button.</summary>
    public string? CloseButtonText
    {
        get => _close.Content as string;
        set => _close.Content = value;
    }

    /// <summary>Whether the primary button can be used.</summary>
    public bool IsPrimaryButtonEnabled
    {
        get => _primary.IsEnabled;
        set => _primary.IsEnabled = value;
    }

    /// <summary>Opens the dialog over a control's window, and says whether the primary button answered it.</summary>
    /// <remarks>See <see cref="Ask"/> for a control that is no longer in a window.</remarks>
    public async Task<bool> ShowAsync(Control over) => await Ask(over) == DialogAnswer.Primary;

    /// <summary>Opens the dialog over a control's window, and says which button answered it.</summary>
    /// <remarks>
    /// Work that ends in a dialog often finishes after a wait, by which time
    /// the window may have closed to the notification area or the mini panel
    /// may have gone. With no window to open over, the dialog is not shown:
    /// what it would have said goes to the app log, and it answers as if
    /// closed, which every caller takes as the safe choice.
    /// </remarks>
    public async Task<DialogAnswer> Ask(Control over)
    {
        if (TopLevel.GetTopLevel(over) is not Window owner)
        {
            AppLog.Write($"dialog: not shown, as the window it was for has gone: {Heading} {(Body as TextBlock)?.Text}");
            return DialogAnswer.None;
        }
        return await ShowDialog<DialogAnswer>(owner);
    }
}
