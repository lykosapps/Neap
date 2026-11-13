using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Neap.Core.Connection;

namespace Neap.Desktop.Controls;

/// <summary>
/// A section of controls that folds into one plain card while what it needs
/// cannot be reached: the headset switched off or out of range, or its
/// settings on a transmitter that has been unplugged.
/// </summary>
/// <remarks>
/// <para>
/// Greyed rows with dashes read as broken, and a dash looks like "failed to
/// load". Instead the rows are hidden, and one card says what they are, why
/// they are not shown, and, when only the settings are out of reach, that the
/// headset is still using them.
/// </para>
/// <para>
/// Set <see cref="Explain"/> to false for a lone row inside a section that
/// stays, so it folds away without a card of its own.
/// </para>
/// </remarks>
public sealed class HeadsetSection : StackPanel
{
    public static readonly StyledProperty<string?> WhatProperty =
        AvaloniaProperty.Register<HeadsetSection, string?>(nameof(What), "");

    public static readonly StyledProperty<string?> WhyProperty =
        AvaloniaProperty.Register<HeadsetSection, string?>(nameof(Why), "");

    public static readonly StyledProperty<string?> WhyOffProperty =
        AvaloniaProperty.Register<HeadsetSection, string?>(nameof(WhyOff), "");

    public static readonly StyledProperty<bool> SoundOnlyProperty =
        AvaloniaProperty.Register<HeadsetSection, bool>(nameof(SoundOnly));

    public static readonly StyledProperty<bool> ExplainProperty =
        AvaloniaProperty.Register<HeadsetSection, bool>(nameof(Explain), defaultValue: true);

    private SettingsCard? _card;
    private StackPanel? _body;

    /// <summary>What is folded away, in the words a person would use.</summary>
    public string? What
    {
        get => GetValue(WhatProperty);
        set => SetValue(WhatProperty, value);
    }

    /// <summary>
    /// Text that replaces the usual explanation, for things that are read
    /// rather than set: firmware and serial numbers are not "still being
    /// used".
    /// </summary>
    public string? Why
    {
        get => GetValue(WhyProperty);
        set => SetValue(WhyProperty, value);
    }

    /// <summary>The explanation used instead when the headset is switched off or out of range.</summary>
    public string? WhyOff
    {
        get => GetValue(WhyOffProperty);
        set => SetValue(WhyOffProperty, value);
    }

    /// <summary>
    /// Whether the section needs only the system's sound device for the
    /// headset, not the headset's settings.
    /// </summary>
    /// <remarks>
    /// For the system's volume and microphone level, spatial sound, the audio
    /// format and the mix. A dock or transmitter keeps the sound device while
    /// the headset is off or out of range, so these stay open then, and only
    /// the headset's own rows inside fold, each in a section of its own. See
    /// <see cref="SectionFold"/>.
    /// </remarks>
    public bool SoundOnly
    {
        get => GetValue(SoundOnlyProperty);
        set => SetValue(SoundOnlyProperty, value);
    }

    /// <summary>Whether a card explains the folded section; false folds it away without one.</summary>
    public bool Explain
    {
        get => GetValue(ExplainProperty);
        set => SetValue(ExplainProperty, value);
    }

    protected override Type StyleKeyOverride => typeof(StackPanel);

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        AppServices.Headset.StatusChanged += OnStatus;
        AppServices.Headset.AccessChanged += Paint;
        Paint();
    }

    protected override void OnUnloaded(RoutedEventArgs e)
    {
        base.OnUnloaded(e);
        AppServices.Headset.StatusChanged -= OnStatus;
        AppServices.Headset.AccessChanged -= Paint;
    }

    private void OnStatus(HeadsetStatus status) => Paint();

    /// <summary>
    /// Moves the section's content into one panel on first use, so folding
    /// shows or hides that panel and never touches the rows, whose visibility
    /// is theirs to set.
    /// </summary>
    private void Gather()
    {
        if (_body is not null) return;
        _body = new StackPanel { Spacing = Spacing };
        var content = Children.ToList();
        Children.Clear();
        foreach (var child in content) _body.Children.Add(child);

        if (Explain)
        {
            _card = new SettingsCard
            {
                Header = What,
                Icon = (Avalonia.Media.Geometry)Application.Current!.FindResource("IconInfo")!,
                IsVisible = false,
                Margin = new Thickness(0, Space.M, 0, 0),
            };
            Children.Add(_card);
        }
        Children.Add(_body);
    }

    private void Paint()
    {
        var status = AppServices.Headset.Status;
        var fold = SectionFold.For(status, SoundOnly, AppServices.Headset.AccessDenied);
        if (fold == Fold.Unchanged) return;
        Gather();

        _body!.IsVisible = fold == Fold.Open;

        // With nothing to show, the section leaves the layout altogether, so
        // the panel around it does not keep a gap for it.
        IsVisible = _body.IsVisible || _card is not null;
        if (_card is null) return;

        // Worded for which it is: "switch it off and on" means nothing to a
        // headset that is already off.
        _card.Description = fold == Fold.Off
            ? (WhyOff is { Length: > 0 } off ? off : Strings.Get("Section_WhyOff"))
            : (Why is { Length: > 0 } why ? why : Strings.Get("Section_Why"));
        _card.IsVisible = fold is Fold.Off or Fold.Unreachable;
    }
}
