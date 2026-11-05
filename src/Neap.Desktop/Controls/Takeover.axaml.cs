using Avalonia.Automation;
using Avalonia.Controls;

namespace Neap.Desktop.Controls;

/// <summary>Work that must not be left part way, shown over the whole main window until it ends and the person closes it.</summary>
/// <remarks>
/// <para>
/// A dialog window of its own can be minimised, lost behind other windows,
/// or, on some desktops, left while the app behind it still works. This is
/// part of the main window instead, so it goes wherever the window goes, and
/// the window switches everything else off while it is held
/// (<see cref="HeldChanged"/>).
/// </para>
/// <para>
/// The card takes the keyboard focus as it opens, so a screen reader says
/// what is under way, and Close takes it once the work has ended.
/// </para>
/// </remarks>
public partial class Takeover : UserControl
{
    private TaskCompletionSource? _closed;

    public Takeover()
    {
        InitializeComponent();
        CloseButton.Click += (_, _) => Release();
    }

    /// <summary>The takeover was shown, with true, or closed, with false.</summary>
    public event Action<bool>? HeldChanged;

    /// <summary>Gets whether the takeover is showing.</summary>
    public bool Held => _closed is not null;

    /// <summary>What is under way, or what was done, said over the work.</summary>
    public string? Heading
    {
        get => HeadingText.Text;
        set
        {
            HeadingText.Text = value;
            AutomationProperties.SetName(Card, value);
        }
    }

    /// <summary>Whether the work has ended, so Close is offered.</summary>
    public bool CanClose
    {
        get => CloseButton.IsVisible;
        set
        {
            CloseButton.IsVisible = value;
            if (value) CloseButton.Focus();
        }
    }

    /// <summary>Shows the work over the window, and finishes once the person closes it.</summary>
    /// <exception cref="InvalidOperationException">Something is already showing.</exception>
    public Task Hold(string heading, Control body)
    {
        if (_closed is not null) throw new InvalidOperationException("the takeover is already showing");
        _closed = new TaskCompletionSource();
        Heading = heading;
        Body.Content = body;
        CanClose = false;
        IsVisible = true;
        HeldChanged?.Invoke(true);
        Card.Focus();
        return _closed.Task;
    }

    private void Release()
    {
        if (_closed is not { } closed) return;
        _closed = null;
        IsVisible = false;
        Body.Content = null;
        HeldChanged?.Invoke(false);
        closed.SetResult();
    }
}
