using System.Globalization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using StealthPro.App.Services;
using StealthPro.Core.Connection;
using StealthPro.Core.Presets;
using Windows.Globalization.NumberFormatting;

namespace StealthPro.App.Controls;

/// <summary>
/// Decibels the way an equaliser reads them: one decimal place, and a sign
/// on anything that is not zero.
///
/// A boost written "3.0" beside a cut written "-3.0" does not look like a
/// pair of opposites at a glance; "+3.0" and "−3.0" do. Typing is forgiving
/// in the other direction — "3", "3.5", "-3", "+3 dB" all land.
/// </summary>
internal sealed class DecibelFormatter : INumberFormatter2, INumberParser
{
    public string FormatInt(long value) => FormatDouble(value);

    public string FormatUInt(ulong value) => FormatDouble(value);

    public string FormatDouble(double value) => Db.Text((int)Math.Round(value * 10));

    public double? ParseDouble(string text) => Parse(text);

    public long? ParseInt(string text) => Parse(text) is double d ? (long)Math.Round(d) : null;

    public ulong? ParseUInt(string text) =>
        Parse(text) is double d and >= 0 ? (ulong)Math.Round(d) : null;

    private static double? Parse(string text)
    {
        string cleaned = new(text.Replace('−', '-').Replace(',', '.')
            .Where(c => char.IsDigit(c) || c is '.' or '-' or '+').ToArray());
        return double.TryParse(cleaned, NumberStyles.Float, CultureInfo.InvariantCulture,
            out double db) ? db : null;
    }
}

/// <summary>
/// One equaliser bank: its presets, and the curve in front of them.
///
/// What this does beyond Swarm, and why each one is here:
///
/// <b>It shows a shape.</b> The bands are a curve you drag, not ten separate
/// sliders. Ten sliders make you read ten numbers to see that the mids are
/// scooped; the curve says it at a glance, and the 0 dB line says which way
/// is up. See <see cref="EqualiserCurve"/>.
///
/// <b>Type a number.</b> Dragging to exactly -3.0 dB is a game, not a
/// control. Every band still has its own field, and arrow keys move it —
/// which is also how the equaliser is reachable without a mouse.
///
/// <b>Put one band back.</b> Where the curve has left the stored preset, the
/// preset is drawn behind it and each moved band gets a ring on it; clicking
/// the ring is the way back for that band alone.
///
/// <b>Say when it has been changed.</b> The headset clears its
/// selected-preset value the moment a band is touched, so without this a
/// curve you have edited is indistinguishable from the preset it came from.
///
/// <b>Show the empty slots.</b> There are five, and how many are left is
/// part of deciding whether to save over one.
///
/// Deleting confirms in place: the slot's own face becomes Delete and
/// Cancel. An earlier version put a dialog in front of it, and a version
/// before that added a second button to explain the first — both were more
/// than the moment needs.
/// </summary>
public sealed partial class EqualiserPanel : UserControl
{
    private readonly List<BandCell> _cells = new();
    private readonly DecibelFormatter _decibels = new();
    private BankState? _state;
    private bool _painting;
    private string? _confirmingDelete;

    private sealed record BandCell(NumberBox Field);

    public EqualiserPanel()
    {
        InitializeComponent();
        DiscardButton.Click += async (_, _) => await Discard();
        SaveButton.Click += async (_, _) => await SaveAsNew();
        OverwriteButton.Click += async (_, _) => await Overwrite();
        Loaded += async (_, _) =>
        {
            AppServices.Headset.Changed += Paint;
            ShowWaiting(true);
            BuildBands();
            await Reload();

            // A headset that turns up late still gets its presets read, once.
            if (_state is null)
                AppServices.Headset.StatusChanged += OnStatusChanged;
        };
        Unloaded += (_, _) =>
        {
            AppServices.Headset.Changed -= Paint;
            AppServices.Headset.StatusChanged -= OnStatusChanged;
        };
    }

    private async void OnStatusChanged(HeadsetStatus status)
    {
        if (status.Link != Link.Connected) return;
        AppServices.Headset.StatusChanged -= OnStatusChanged;
        await Reload();
    }

    public static readonly DependencyProperty BankProperty = DependencyProperty.Register(
        nameof(Bank), typeof(Bank), typeof(EqualiserPanel), new PropertyMetadata(Core.Presets.Bank.Game));

    public Bank Bank
    {
        get => (Bank)GetValue(BankProperty);
        set => SetValue(BankProperty, value);
    }

    private async Task Reload()
    {
        try { _state = await AppServices.Presets.Load(Bank); }
        catch
        {
            // The headset is not answering. The status strip already says so;
            // what matters here is not showing a curve we cannot vouch for.
            ShowWaiting(true);
            return;
        }
        BuildSlots();
        Paint();
    }

    private void ShowWaiting(bool waiting)
    {
        Waiting.Visibility = waiting ? Visibility.Visible : Visibility.Collapsed;
        Curve.Visibility = waiting ? Visibility.Collapsed : Visibility.Visible;
        Slots.Visibility = waiting ? Visibility.Collapsed : Visibility.Visible;
        SaveButton.Visibility = waiting ? Visibility.Collapsed : Visibility.Visible;
    }

    // -- the bands ---------------------------------------------------------

    private void BuildBands()
    {
        var spec = PresetStore.Spec(Bank);

        Response.Minimum = PresetService.BandFloor;
        Response.Maximum = PresetService.BandCeiling;
        Response.Frequencies = spec.Frequencies;
        Response.BandChanged += (index, tenths) =>
        {
            AppServices.Presets.SetBand(Bank, index, tenths);
            Paint();
        };
        Response.BandReverted += index =>
        {
            AppServices.Presets.RevertBand(Bank, index);
            Paint();
        };

        Bands.ColumnDefinitions.Clear();
        Bands.Children.Clear();
        _cells.Clear();

        for (int i = 0; i < spec.Frequencies.Count; i++)
        {
            // Star columns, so the readouts stay under their own points at
            // any window width.
            Bands.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var field = new NumberBox
            {
                Minimum = PresetService.BandFloor / 10.0,
                Maximum = PresetService.BandCeiling / 10.0,
                SmallChange = 0.5,
                LargeChange = 1,
                NumberFormatter = _decibels,
                // No spin buttons. Compact floats them out over the next
                // band the moment a field takes focus, which covered the
                // neighbour and squeezed the value being edited down to its
                // last digit. Nothing is lost: arrow keys step by
                // SmallChange whenever the field has focus, buttons or not.
                SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Hidden,
                ValidationMode = NumberBoxValidationMode.InvalidInputOverwritten,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(3, 0, 3, 0),
            };
            // The sliders this replaces had no name of their own, so a screen
            // reader read out ten identical controls.
            AutomationProperties.SetName(field, spec.Frequencies[i]);

            var hz = new TextBlock
            {
                Text = spec.Frequencies[i],
                HorizontalAlignment = HorizontalAlignment.Center,
                Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"],
                Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
            };

            int index = i;
            field.ValueChanged += (sender, args) =>
            {
                if (_painting) return;
                if (double.IsNaN(args.NewValue))
                {
                    // Cleared rather than changed. Put back what is there.
                    Paint();
                    return;
                }
                AppServices.Presets.SetBand(Bank, index, (int)Math.Round(args.NewValue * 10));
                Paint();
            };

            var column = new StackPanel { Spacing = 6 };
            column.Children.Add(field);
            column.Children.Add(hz);
            Grid.SetColumn(column, i);
            Bands.Children.Add(column);
            _cells.Add(new BandCell(field));
        }
    }

    // -- the slots ---------------------------------------------------------

    private void BuildSlots()
    {
        Slots.Items.Clear();
        if (_state is null) return;

        foreach (var preset in _state.Presets)
            Slots.Items.Add(SlotChip(preset));

        foreach (int slot in _state.FreeSlots)
            Slots.Items.Add(EmptyChip());
    }

    private UIElement SlotChip(Preset preset)
    {
        var face = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        face.Children.Add(new TextBlock { Text = preset.Name, VerticalAlignment = VerticalAlignment.Center });

        var chip = new Button
        {
            Content = face,
            Padding = new Thickness(14, 8, 14, 8),
            Tag = preset,
        };
        // A chip's content is a panel rather than a string, so it has no name
        // of its own — every preset announced as an unlabelled button.
        AutomationProperties.SetName(chip, preset.Name);
        chip.Click += async (_, _) =>
        {
            if (_confirmingDelete is not null) return;
            await AppServices.Presets.Select(Bank, preset);
            Paint();
        };

        // Only a preset you made can be removed, and only once it is the one
        // you are on — deleting something you are not listening to is a
        // reach for a problem nobody has.
        if (preset.Custom)
        {
            var remove = new Button
            {
                Content = new FontIcon { Glyph = "\uE711", FontSize = 11 },
                Padding = new Thickness(4, 0, 4, 0),
                Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
                BorderThickness = new Thickness(0),
                Visibility = Visibility.Collapsed,
                Tag = "remove",
            };
            AutomationProperties.SetName(remove, $"Delete {preset.Name}");
            remove.Click += (_, args) =>
            {
                _confirmingDelete = preset.Name;
                Paint();
            };
            face.Children.Add(remove);
        }

        return chip;
    }

    /// <summary>
    /// The chip becomes the question. No dialog, no second explanation: at
    /// that moment the only two things worth offering are going through with
    /// it and not.
    /// </summary>
    private UIElement ConfirmChip(Preset preset)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
        var delete = new Button { Content = "Delete", Style = (Style)Application.Current.Resources["AccentButtonStyle"] };
        var cancel = new Button { Content = "Cancel" };
        // "Delete" and "Cancel" on their own do not say what of.
        AutomationProperties.SetName(delete, $"Delete {preset.Name}");
        AutomationProperties.SetName(cancel, $"Keep {preset.Name}");
        delete.Click += async (_, _) =>
        {
            _confirmingDelete = null;
            await AppServices.Presets.Delete(Bank, preset.Name);
            await Reload();
        };
        cancel.Click += (_, _) => { _confirmingDelete = null; Paint(); };
        row.Children.Add(delete);
        row.Children.Add(cancel);
        return row;
    }

    private static UIElement EmptyChip() => new Border
    {
        Padding = new Thickness(14, 8, 14, 8),
        BorderBrush = (Brush)Application.Current.Resources["ControlStrokeColorDefaultBrush"],
        BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(4),
        Child = new TextBlock
        {
            Text = "Empty slot",
            Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"],
            Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
        },
    };

    // -- saving and discarding ---------------------------------------------

    private async Task Discard()
    {
        await AppServices.Presets.Discard(Bank);
        Paint();
    }

    private static bool Same(string a, string b) =>
        string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Keep this curve as a preset of its own.
    ///
    /// <b>Saving and overwriting are two buttons, not one.</b> They were one
    /// twice over and both were wrong. First the button changed itself from
    /// "Save as new" to "Save to Mud cut" depending on what was selected, so
    /// the destructive action wore the safe one's clothes and sat in its
    /// place. Then it became a single "Save preset" whose dialog let you
    /// change the name to get a new one instead — which works, but nothing
    /// on screen says so, and a control whose second meaning is only
    /// reachable by guessing is not offering it. If there are two things a
    /// person might want here, there are two buttons.
    /// </summary>
    private async Task SaveAsNew()
    {
        if (_state is null) return;

        string name = await AskForName();
        if (name.Length == 0) return;

        string? trouble = await AppServices.Presets.Save(Bank, name, null);
        if (trouble is not null) { await Complain(trouble); return; }
        await Reload();
    }

    /// <summary>
    /// Write the curve back over the preset it came from.
    ///
    /// Named rather than confirmed: the button says which preset it is about
    /// to replace, and that is the part the old one was missing. Replacing is
    /// a delete followed by a write, so it destroys more than deleting does —
    /// but a person who has pressed a button reading "Overwrite Mud cut" has
    /// said what they meant, and a dialog on top of that is the second
    /// explanation this card has already thrown away once.
    /// </summary>
    private async Task Overwrite()
    {
        if (_state?.Baseline is not { Custom: true } baseline) return;

        string? trouble = await AppServices.Presets.Save(Bank, baseline.Name, baseline.Name);
        if (trouble is not null) { await Complain(trouble); return; }
        await Reload();
    }

    /// <summary>
    /// Name the new preset.
    ///
    /// Everything that would refuse the save says so here, while there is
    /// still something to do about it, rather than as an error once the name
    /// has been typed: no slots left, a name already taken, a name belonging
    /// to one of the headset's own presets, or one at the length ceiling.
    /// A name already taken points at the other button rather than just
    /// refusing, because wanting to replace that preset is the likeliest
    /// reason to be typing its name.
    /// </summary>
    private async Task<string> AskForName()
    {
        var field = new TextBox
        {
            PlaceholderText = "Name",
            MaxLength = PresetStore.MaxNameLength,
        };

        var note = new TextBlock
        {
            Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"],
            Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
            TextWrapping = TextWrapping.Wrap,
        };

        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = "Save as a new preset",
            Content = new StackPanel { Spacing = 8, Children = { field, note } },
            PrimaryButtonText = "Save",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
        };

        void Judge()
        {
            if (_state is not { } state) return;
            string typed = field.Text.Trim();
            var taken = state.Custom.FirstOrDefault(p => Same(p.Name, typed));
            bool factory = PresetStore.Factory(Bank).Any(p => Same(p.Name, typed));
            int free = state.FreeSlots.Count;

            if (typed.Length == 0)
            {
                note.Text = "Give it a name.";
                dialog.IsPrimaryButtonEnabled = false;
            }
            else if (factory)
            {
                note.Text = $"{typed} is one of the headset's own presets.";
                dialog.IsPrimaryButtonEnabled = false;
            }
            else if (taken is not null)
            {
                note.Text = $"You already have a preset called {taken.Name}. "
                          + "To replace it, close this and use Overwrite.";
                dialog.IsPrimaryButtonEnabled = false;
            }
            else if (free == 0)
            {
                note.Text = "All five slots are full. Delete one first, or overwrite "
                          + "the preset this came from.";
                dialog.IsPrimaryButtonEnabled = false;
            }
            else
            {
                note.Text = $"{free} of 5 slots free."
                          + (field.Text.Length >= PresetStore.MaxNameLength
                             ? " That is as long as a name can be." : "");
                dialog.IsPrimaryButtonEnabled = true;
            }
        }

        field.TextChanged += (_, _) => Judge();
        Judge();
        field.SelectAll();

        var answer = await dialog.ShowAsync();
        return answer == ContentDialogResult.Primary ? field.Text.Trim() : "";
    }

    private async Task Complain(string trouble) => await new ContentDialog
    {
        XamlRoot = XamlRoot,
        Title = "Could not save",
        Content = trouble,
        CloseButtonText = "OK",
    }.ShowAsync();

    // -- painting ----------------------------------------------------------

    private void Paint()
    {
        if (_state is null) return;

        // The presets can be listed while the headset is still not saying
        // where its bands are — a transmitter that is plugged in with the
        // headset switched off answers the preset slots and nothing else.
        // Showing the card anyway gives a blank plot over ten empty fields,
        // which reads as a broken equaliser rather than an absent headset.
        var live = AppServices.Presets.LiveBands(Bank);
        ShowWaiting(live is null);
        if (live is null) return;

        _painting = true;
        try
        {
            for (int i = 0; i < _cells.Count && i < live.Length; i++)
                _cells[i].Field.Value = live[i] / 10.0;

            // The curve is given the preset behind it as well, so it can
            // draw where each band was before it was moved.
            Response.Show(live, _state.Baseline?.Bands.ToArray());

            PresetName.Text = AppServices.Presets.CurrentName(Bank);
            bool edited = AppServices.Presets.IsEdited(Bank);
            EditedPill.Visibility = edited ? Visibility.Visible : Visibility.Collapsed;
            DiscardButton.Visibility = edited ? Visibility.Visible : Visibility.Collapsed;

            // Only when there is something to save. It used to be enabled
            // whenever a slot was free, so pressing it with nothing changed
            // spent one of five slots on a copy of a preset already there.
            // The exception is a curve we cannot place: if we never worked
            // out which preset it came from, saving it is the way to keep it.
            SaveButton.IsEnabled = edited || _state.Baseline is null;

            // Overwrite only exists when there is a preset of yours to
            // overwrite. On a factory preset there is nothing to write back
            // to, so the button is not there rather than there and refusing.
            var over = _state.Baseline;
            bool canOverwrite = edited && over is { Custom: true };
            OverwriteButton.Visibility = canOverwrite ? Visibility.Visible : Visibility.Collapsed;
            if (canOverwrite) OverwriteButton.Content = $"Overwrite {over!.Name}";

            // The accent marks the likely intent, which moves: refining a
            // preset of your own usually means keeping it, and on one of the
            // headset's own there is only one thing you can do.
            SaveButton.Style = canOverwrite
                ? (Style)Application.Current.Resources["DefaultButtonStyle"]
                : (Style)Application.Current.Resources["AccentButtonStyle"];

            PaintSlotStates();
        }
        finally { _painting = false; }
    }

    private void PaintSlotStates()
    {
        if (_state is null) return;
        var baseline = _state.Baseline;
        int index = 0;
        foreach (var preset in _state.Presets)
        {
            if (index >= Slots.Items.Count) break;
            bool selected = baseline is not null && baseline.Id == preset.Id;

            if (_confirmingDelete == preset.Name)
            {
                Slots.Items[index] = ConfirmChip(preset);
            }
            else
            {
                if (Slots.Items[index] is not Button chip || chip.Tag is not Preset held || held.Id != preset.Id)
                    Slots.Items[index] = SlotChip(preset);

                if (Slots.Items[index] is Button button)
                {
                    button.Style = (Style)Application.Current.Resources[
                        selected ? "AccentButtonStyle" : "DefaultButtonStyle"];
                    if (button.Content is StackPanel face)
                        foreach (var child in face.Children)
                            if (child is Button { Tag: "remove" } remove)
                                remove.Visibility = selected ? Visibility.Visible : Visibility.Collapsed;
                }
            }
            index++;
        }
    }
}
