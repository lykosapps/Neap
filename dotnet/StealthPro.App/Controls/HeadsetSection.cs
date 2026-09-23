using CommunityToolkit.WinUI.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using StealthPro.App.Services;
using StealthPro.Core.Connection;

namespace StealthPro.App.Controls;

/// <summary>
/// A section of settings that folds into one plain card while the headset
/// cannot be reached: switched off, out of range, or its settings on a
/// transmitter that has been unplugged.
/// </summary>
/// <remarks>
/// <para>
/// Greyed rows with dashes read as broken, and a dash looks like "failed to
/// load". Instead the rows are hidden, and one card says what they are, why
/// they are not shown, and, when only the settings are out of reach, that the
/// headset is still using them.
/// </para>
/// <para>
/// Wrap the sections in markup:
/// <code>&lt;c:HeadsetSection What="Noise cancellation and the equaliser"&gt; … &lt;/c:HeadsetSection&gt;</code>
/// Set <see cref="Explain"/> to false for a lone row inside a section that
/// stays, so it folds away without a card of its own.
/// </para>
/// </remarks>
public sealed class HeadsetSection : StackPanel
{
    private SettingsCard? _card;
    private StackPanel? _body;

    public HeadsetSection()
    {
        Loaded += (_, _) =>
        {
            AppServices.Headset.StatusChanged += OnStatus;
            Paint();
        };
        Unloaded += (_, _) => AppServices.Headset.StatusChanged -= OnStatus;
    }

    public static readonly DependencyProperty WhatProperty = DependencyProperty.Register(
        nameof(What), typeof(string), typeof(HeadsetSection), new PropertyMetadata(""));

    /// <summary>Gets or sets what is folded away, in the words a person would use.</summary>
    public string What
    {
        get => (string)GetValue(WhatProperty);
        set => SetValue(WhatProperty, value);
    }

    public static readonly DependencyProperty WhyProperty = DependencyProperty.Register(
        nameof(Why), typeof(string), typeof(HeadsetSection), new PropertyMetadata(""));

    /// <summary>
    /// Gets or sets text that replaces the usual explanation, for things that
    /// are read rather than set: firmware and serial numbers are not "still
    /// being used".
    /// </summary>
    public string Why
    {
        get => (string)GetValue(WhyProperty);
        set => SetValue(WhyProperty, value);
    }

    public static readonly DependencyProperty WhyOffProperty = DependencyProperty.Register(
        nameof(WhyOff), typeof(string), typeof(HeadsetSection), new PropertyMetadata(""));

    /// <summary>Gets or sets the explanation used instead when the headset is switched off or out of range.</summary>
    public string WhyOff
    {
        get => (string)GetValue(WhyOffProperty);
        set => SetValue(WhyOffProperty, value);
    }

    public static readonly DependencyProperty WhenOffProperty = DependencyProperty.Register(
        nameof(WhenOff), typeof(bool), typeof(HeadsetSection), new PropertyMetadata(false));

    /// <summary>
    /// Gets or sets whether to fold only when the headset is off, out of range
    /// or not plugged in, and stay open while only its settings are out of
    /// reach.
    /// </summary>
    /// <remarks>
    /// For what goes on working without the headset's settings: the mix,
    /// Windows' volume and microphone level, and the audio format. With the
    /// headset off they would act on the PC's own devices, so then they fold
    /// too.
    /// </remarks>
    public bool WhenOff
    {
        get => (bool)GetValue(WhenOffProperty);
        set => SetValue(WhenOffProperty, value);
    }

    public static readonly DependencyProperty ExplainProperty = DependencyProperty.Register(
        nameof(Explain), typeof(bool), typeof(HeadsetSection), new PropertyMetadata(true));

    /// <summary>Gets or sets whether a card explains the folded section; false folds it away without one.</summary>
    public bool Explain
    {
        get => (bool)GetValue(ExplainProperty);
        set => SetValue(ExplainProperty, value);
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
        _body = new StackPanel();
        var content = Children.ToList();
        Children.Clear();
        foreach (var child in content) _body.Children.Add(child);

        if (Explain)
        {
            _card = new SettingsCard
            {
                Header = What,
                HeaderIcon = new FontIcon { Glyph = "" },
                Visibility = Visibility.Collapsed,
                Margin = new Thickness(0, 12, 0, 0),
            };
            Children.Add(_card);
        }
        Children.Add(_body);
    }

    private void Paint()
    {
        var status = AppServices.Headset.Status;

        // Connecting keeps the page as it was. It lasts a few seconds, and
        // folding for it would make every page jump twice.
        if (status.Link == Link.Connecting) return;
        Gather();

        bool absent = status.Link == Link.Absent;
        bool fold = absent || status.NotConnected || (!WhenOff && status.SettingsUnreachable);

        _body!.Visibility = fold ? Visibility.Collapsed : Visibility.Visible;
        if (_card is null) return;

        // Worded for which it is: "switch it off and on" means nothing to a
        // headset that is already off.
        _card.Description = status.NotConnected
            ? (WhyOff.Length > 0 ? WhyOff : Strings.Get("Section_WhyOff"))
            : (Why.Length > 0 ? Why : Strings.Get("Section_Why"));

        // Nothing plugged in is said once, at the top of the page; a card
        // here as well would say it twice.
        _card.Visibility = fold && !absent ? Visibility.Visible : Visibility.Collapsed;
    }
}
