using CommunityToolkit.WinUI.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using StealthPro.App.Services;

namespace StealthPro.App.Controls;

/// <summary>
/// Sections the app cannot reach right now, folded into one plain explanation
/// whenever a transmitter is plugged in but nothing answers for the headset's
/// settings — whichever transmitter it is. See
/// <see cref="HeadsetStatus.SettingsUnreachable"/>.
///
/// <b>The fix is not the Charging Dock.</b> This first said these needed the
/// dock. They do not: the headset keeps its controls on the transmitter it
/// was switched on with, and switched on with only the USB Transmitter it
/// answers for everything through it — settings, the chat wheel, the lot. The
/// state this covers comes from switching on with the dock and moving over
/// with CrossPlay, and switching the headset off and on is what ends it.
///
/// <b>A page of grey rows reads as broken.</b> On the USB Transmitter alone
/// every headset setting came back empty, so each row sat greyed with a dash
/// — and a dash looks like "failed to load". The Controls page was grey from
/// top to bottom. For somebody whose USB Transmitter is simply how their
/// headset is set up, the app looked half-installed when nothing was wrong:
/// it just offers less there.
///
/// So the rows go, and one card says what they are, why they are not here,
/// and — the part nobody had been told — that the headset is still using
/// them. What does work on the USB Transmitter is left at full strength.
///
/// Wrap the sections in markup:
/// <code>&lt;c:NeedsDock What="Noise cancellation and the equaliser"&gt; … &lt;/c:NeedsDock&gt;</code>
/// Set <see cref="Explain"/> to false for a lone row inside a section that
/// stays, so it folds away without a card of its own; the section's main
/// card covers it.
/// </summary>
public sealed class NeedsDock : StackPanel
{
    private SettingsCard? _card;
    private readonly Dictionary<UIElement, Visibility> _was = new();
    private bool _folded;

    public NeedsDock()
    {
        Spacing = 0;
        Loaded += (_, _) =>
        {
            AppServices.Headset.StatusChanged += OnStatus;
            Paint();
        };
        Unloaded += (_, _) => AppServices.Headset.StatusChanged -= OnStatus;
    }

    public static readonly DependencyProperty WhatProperty = DependencyProperty.Register(
        nameof(What), typeof(string), typeof(NeedsDock), new PropertyMetadata(""));

    /// <summary>What is folded away, in the words a person would use.</summary>
    public string What
    {
        get => (string)GetValue(WhatProperty);
        set => SetValue(WhatProperty, value);
    }

    public static readonly DependencyProperty WhyProperty = DependencyProperty.Register(
        nameof(Why), typeof(string), typeof(NeedsDock), new PropertyMetadata(""));

    /// <summary>
    /// Replaces the usual explanation, for things that are read rather than
    /// set — firmware and serial numbers are not "still being used".
    /// </summary>
    public string Why
    {
        get => (string)GetValue(WhyProperty);
        set => SetValue(WhyProperty, value);
    }

    public static readonly DependencyProperty WhyOffProperty = DependencyProperty.Register(
        nameof(WhyOff), typeof(string), typeof(NeedsDock), new PropertyMetadata(""));

    /// <summary>
    /// What to say instead when the headset is switched off or out of range,
    /// rather than on with its settings out of reach.
    /// </summary>
    public string WhyOff
    {
        get => (string)GetValue(WhyOffProperty);
        set => SetValue(WhyOffProperty, value);
    }

    public static readonly DependencyProperty WhenOffProperty = DependencyProperty.Register(
        nameof(WhenOff), typeof(bool), typeof(NeedsDock), new PropertyMetadata(false));

    /// <summary>
    /// Fold only when the headset is off, out of range or not plugged in,
    /// not whenever its settings are out of reach — for what goes on working
    /// then: the mix, Windows' volume and microphone level, and the audio
    /// format.
    ///
    /// <b>Off, they were controlling something else.</b> With the headset
    /// switched off on its cable its sound device leaves Windows, and the
    /// volume slider on Audio went on working — on the PC's own speakers, at
    /// their level, without a word. So with the headset off a page is one
    /// card, whatever is on it.
    /// </summary>
    public bool WhenOff
    {
        get => (bool)GetValue(WhenOffProperty);
        set => SetValue(WhenOffProperty, value);
    }

    public static readonly DependencyProperty ExplainProperty = DependencyProperty.Register(
        nameof(Explain), typeof(bool), typeof(NeedsDock), new PropertyMetadata(true));

    /// <summary>False to fold away without a card.</summary>
    public bool Explain
    {
        get => (bool)GetValue(ExplainProperty);
        set => SetValue(ExplainProperty, value);
    }

    private void OnStatus(HeadsetStatus status) => Paint();

    private void Paint()
    {
        var status = AppServices.Headset.Status;

        // <b>Connecting keeps the page as it was.</b> It lasts a few seconds —
        // the headset switching on over its cable, a transmitter changing —
        // and folding or unfolding for it made every page jump twice.
        if (status.Link == Link.Connecting) return;

        bool absent = status.Link == Link.Absent;

        // With nothing plugged in there is nothing to show either: every row
        // would sit empty, which reads as broken.
        bool fold = absent || status.NotConnected
                    || (!WhenOff && status.SettingsUnreachable);

        // The same card for both, worded for which it is: "switch it off and
        // on" means nothing to a headset that is already off.
        string description = status.NotConnected
            ? (WhyOff.Length > 0 ? WhyOff : "Switch your headset on to see and change these.")
            : (Why.Length > 0
                ? Why
                : "These are set on the headset itself, which keeps using them. Switch "
                  + "the headset off and on again to change them here.");

        if (Explain && _card is null)
        {
            _card = new SettingsCard
            {
                Header = What,
                Description = description,
                HeaderIcon = new FontIcon { Glyph = "" },
                Visibility = Visibility.Collapsed,
                Margin = new Thickness(0, 12, 0, 0),
            };
            Children.Insert(0, _card);
        }
        if (_card is not null) _card.Description = description;

        if (fold != _folded)
        {
            foreach (var child in Children)
            {
                if (ReferenceEquals(child, _card)) continue;
                if (fold)
                {
                    // Remember what each child was, so folding back never
                    // shows something that had hidden itself. A child that
                    // hides itself as it goes, like a notice, has to sit
                    // inside a panel here, or this would put it back stale.
                    _was[child] = child.Visibility;
                    child.Visibility = Visibility.Collapsed;
                }
                else
                {
                    child.Visibility = _was.TryGetValue(child, out var before) ? before : Visibility.Visible;
                }
            }
            _folded = fold;
        }

        // Nothing plugged in is said once, at the top of the page; a card
        // here as well would say it twice.
        if (_card is not null)
            _card.Visibility = fold && !absent ? Visibility.Visible : Visibility.Collapsed;
    }
}
