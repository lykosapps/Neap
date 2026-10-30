using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Media;

namespace Neap.Desktop.Controls;

/// <summary>How serious what a <see cref="Notice"/> says is.</summary>
public enum Severity { Informational, Warning }

/// <summary>A sentence the screen has to say in words, which comes and goes.</summary>
/// <remarks>
/// <para>
/// Hidden when closed, and takes no room: a closed one left in a stack would
/// still be spaced as a child of it, which leaves a blank band.
/// </para>
/// <para>
/// Its words are a live region, politely for a note and at once for a
/// warning, and are put in only as it opens, so a screen reader says them
/// when it appears and whenever they change, not only someone who sees it.
/// </para>
/// </remarks>
public partial class Notice : UserControl
{
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<Notice, string?>(nameof(Title));

    public static readonly StyledProperty<string?> MessageProperty =
        AvaloniaProperty.Register<Notice, string?>(nameof(Message));

    public static readonly StyledProperty<bool> IsOpenProperty =
        AvaloniaProperty.Register<Notice, bool>(nameof(IsOpen));

    public static readonly StyledProperty<Severity> SeverityProperty =
        AvaloniaProperty.Register<Notice, Severity>(nameof(Severity));

    public static readonly StyledProperty<string?> ActionTextProperty =
        AvaloniaProperty.Register<Notice, string?>(nameof(ActionText));

    public Notice()
    {
        InitializeComponent();
        Action.Click += (_, _) => ActionInvoked?.Invoke(this, EventArgs.Empty);
        IsVisible = false;
        Live(AutomationLiveSetting.Polite);
    }

    public string? Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string? Message
    {
        get => GetValue(MessageProperty);
        set => SetValue(MessageProperty, value);
    }

    public bool IsOpen
    {
        get => GetValue(IsOpenProperty);
        set => SetValue(IsOpenProperty, value);
    }

    public Severity Severity
    {
        get => GetValue(SeverityProperty);
        set => SetValue(SeverityProperty, value);
    }

    /// <summary>The words of a link after the message that does something about it; none for no link.</summary>
    public string? ActionText
    {
        get => GetValue(ActionTextProperty);
        set => SetValue(ActionTextProperty, value);
    }

    /// <summary>The link after the message was chosen.</summary>
    public event EventHandler? ActionInvoked;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TitleProperty || change.Property == MessageProperty || change.Property == IsOpenProperty)
        {
            TitleText.Text = IsOpen ? Title : null;
            TitleText.IsVisible = !string.IsNullOrEmpty(Title);
            MessageText.Text = IsOpen ? Message : null;
            MessageText.IsVisible = !string.IsNullOrEmpty(Message);
            IsVisible = IsOpen;
        }
        else if (change.Property == ActionTextProperty)
        {
            Action.Content = ActionText;
            Action.IsVisible = !string.IsNullOrEmpty(ActionText);
        }
        else if (change.Property == SeverityProperty)
        {
            Frame.Classes.Set("warning", Severity == Severity.Warning);
            Mark.Data = (Geometry)Application.Current!.FindResource(Severity == Severity.Warning ? "IconWarning" : "IconInfo")!;
            Live(Severity == Severity.Warning ? AutomationLiveSetting.Assertive : AutomationLiveSetting.Polite);
        }
    }

    private void Live(AutomationLiveSetting setting)
    {
        AutomationProperties.SetLiveSetting(TitleText, setting);
        AutomationProperties.SetLiveSetting(MessageText, setting);
    }
}
