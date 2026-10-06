using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;

namespace Neap.Desktop.Controls;

/// <summary>A dialog in Neap's colours, with a primary button and a close button.</summary>
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
    private readonly Button _primary = new() { IsDefault = true, MinWidth = 96 };
    private readonly Button _close = new() { IsCancel = true, MinWidth = 96 };
    private readonly ContentControl _body = new();
    private readonly TextBlock _heading = new() { Classes = { "title" } };

    public NeapDialog()
    {
        Width = 440;
        SizeToContent = SizeToContent.Height;
        CanResize = false;
        ShowInTaskbar = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = Application.Current!.FindResource("NeapGroundBrush") as Avalonia.Media.IBrush;

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8,
            Children = { _primary, _close },
        };
        Content = new StackPanel
        {
            Margin = new Thickness(24),
            Spacing = 16,
            Children = { _heading, _body, buttons },
        };

        _close.Click += (_, _) => Close(false);
        _primary.Click += (_, _) => Close(true);
        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape) Close(false);
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
    /// <exception cref="InvalidOperationException">The control is not in a window.</exception>
    public async Task<bool> ShowAsync(Control over)
    {
        var owner = TopLevel.GetTopLevel(over) as Window
            ?? throw new InvalidOperationException("a dialog needs the window it opens over");
        return await ShowDialog<bool>(owner);
    }
}
