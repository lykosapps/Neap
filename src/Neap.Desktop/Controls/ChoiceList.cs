using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Neap.Desktop.Controls;

/// <summary>One row of a list chosen from with a <see cref="ComboBox"/>.</summary>
/// <param name="Name">What the row says, and its name for a screen reader.</param>
/// <param name="Tag">What choosing the row chooses; null for a heading.</param>
public sealed record Choice(string Name, object? Tag)
{
    /// <summary>Makes what is drawn at the row's end, such as a preset's curve.</summary>
    public Func<Control?>? Preview { get; init; }

    /// <summary>The row in use, shown as the box's choice.</summary>
    public bool Chosen { get; init; }

    /// <summary>The row can be deleted, with a delete button at its end.</summary>
    public bool Deletable { get; init; }

    /// <summary>Tags beside the name in the box once the row is chosen, such as Edited.</summary>
    public IReadOnlyList<string> Badges { get; init; } = [];

    /// <summary>The row is asking Delete or Cancel.</summary>
    internal bool Confirming { get; init; }

    /// <summary>Whether the row is a rule setting groups of rows apart.</summary>
    public bool IsRule { get; init; }

    /// <summary>A heading over the rows after it, which cannot be chosen.</summary>
    public static Choice Heading(string name) => new(name, null);

    /// <summary>A rule between groups of rows, a row of its own so it is never inside a row's hover or chosen tint.</summary>
    public static Choice Rule() => new(string.Empty, null) { IsRule = true };

    /// <summary>The row's name, which is what a screen reader says for the box's choice.</summary>
    public override string ToString() => Name;
}

/// <summary>
/// Sets a <see cref="ComboBox"/> to list <see cref="Choice"/> rows the way
/// every list Neap chooses from does.
/// </summary>
/// <remarks>
/// <para>
/// The box is the toolkit's own; this gives it the rows: each row's name and
/// preview, headings and rules that set groups apart, the Bands preset list's
/// marker on the chosen row, and the box naming the choice with its tags. The
/// row in use is the box's choice; with none, the box shows its placeholder.
/// </para>
/// <para>
/// A row the person made has the ten-band equaliser's delete button at its
/// end, shown when the row is pointed at or focused, and asks Delete or Cancel
/// in the row's place, with no dialog and the keyboard on Cancel. The Delete
/// key does the same on a focused row.
/// </para>
/// <para>
/// A row that only acts, such as a starting shape or Load, is chosen and then
/// the box goes back to the row in use.
/// </para>
/// </remarks>
internal sealed class ChoiceList
{
    /// <summary>The width of a row's delete button, as on the ten-band equaliser's rows.</summary>
    private const double DeleteWidth = 32;

    private readonly ComboBox _box;
    private IReadOnlyList<Choice> _choices = [];
    private object? _confirming;
    private bool _filling;

    public ChoiceList(ComboBox box)
    {
        _box = box;
        box.ItemTemplate = new FuncDataTemplate<Choice>((choice, _) => choice is null ? null : choice.Confirming ? Confirm(choice) : Row(choice));
        box.SelectionBoxItemTemplate = new FuncDataTemplate<Choice>((choice, _) => choice is null ? null : Face(choice));
        box.ContainerPrepared += (_, e) =>
        {
            if (e.Container is not ComboBoxItem item || item.DataContext is not Choice choice) return;
            item.IsEnabled = choice.Tag is not null;
            item.Focusable = choice.Tag is not null && !choice.Confirming;
            AutomationProperties.SetName(item, choice.Name);
            // A rule takes only the room it needs; containers are reused, so a row gets its own back.
            item.Classes.Set("rule", choice.IsRule);
            AutomationProperties.SetAccessibilityView(item, choice.IsRule ? AccessibilityView.Raw : AccessibilityView.Control);
        };
        box.SelectionChanged += (_, _) =>
        {
            if (!_filling && box.SelectedItem is Choice { Tag: { } tag }) Choose(tag);
        };
        box.KeyDown += (_, e) =>
        {
            if (e.Key != Key.Delete || (e.Source as Control)?.FindAncestorOfType<ComboBoxItem>(includeSelf: true)?.DataContext is not Choice { Deletable: true, Tag: { } tag }) return;
            e.Handled = true;
            AskToDelete(tag);
        };
        box.DropDownClosed += (_, _) =>
        {
            if (_confirming is null) return;
            _confirming = null;
            Fill();
        };
    }

    /// <summary>A row was chosen, with its tag.</summary>
    public event Action<object>? Chosen;

    /// <summary>A row's deletion was confirmed, with its tag.</summary>
    public event Action<object>? Deleted;

    /// <summary>Lists the rows, with the one marked chosen as the box's choice.</summary>
    public void Show(IReadOnlyList<Choice> choices)
    {
        _choices = choices;
        Fill();
    }

    private void Fill()
    {
        _filling = true;
        try
        {
            var shown = _choices.Select(c => Equals(c.Tag, _confirming) && c.Tag is not null ? c with { Confirming = true } : c).ToList();
            _box.ItemsSource = shown;
            _box.SelectedItem = shown.FirstOrDefault(c => c.Chosen && c.Tag is not null);
        }
        finally { _filling = false; }
    }

    /// <summary>Puts the box back on the row in use and says what was chosen.</summary>
    /// <remarks>Posted, as choosing usually lists the rows again, which cannot happen while the box is still selecting.</remarks>
    private void Choose(object chosen) => Dispatcher.UIThread.Post(() =>
    {
        Fill();
        Chosen?.Invoke(chosen);
    });

    /// <summary>What the box shows for its choice: the name, and its tags.</summary>
    private static Control Face(Choice choice)
    {
        var name = new TextBlock { Text = choice.Name, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
        if (choice.Badges.Count == 0) return name;
        var face = new StackPanel { Classes = { "gaprow" }, Children = { name } };
        foreach (string badge in choice.Badges) face.Children.Add(new Border { Classes = { "tag" }, Child = new TextBlock { Text = badge } });
        return face;
    }

    /// <summary>A row: its marker, name, preview and delete button; or a heading; or a rule.</summary>
    private Control Row(Choice choice)
    {
        if (choice.IsRule)
        {
            var line = new Border { Height = 1 };
            line.Bind(Border.BackgroundProperty, line.GetResourceObservable("NeapPopupStrokeBrush"));
            return line;
        }

        return choice.Tag is not { } tag ? new TextBlock { Text = choice.Name, Classes = { "label" } } : Line(choice, tag);
    }

    /// <summary>A row that can be chosen: the chosen marker, its name, its preview, and its delete button when it has one.</summary>
    private Grid Line(Choice choice, object tag)
    {
        // Every row keeps the delete button's room when any row has one, so the previews line up down the list.
        var room = _choices.Any(c => c.Deletable) ? new GridLength(DeleteWidth) : GridLength.Auto;
        var row = new Grid
        {
            ColumnDefinitions = { new(GridLength.Auto), new(GridLength.Star), new(GridLength.Auto), new(room) },
            ColumnSpacing = Space.S,
            Classes = { "slot" },
        };
        row.Children.Add(new Border { Classes = { "marker" } });
        var words = new TextBlock { Text = choice.Name, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
        Grid.SetColumn(words, 1);
        row.Children.Add(words);
        if (choice.Preview?.Invoke() is { } preview)
        {
            preview.Margin = new Thickness(Space.S, 0, 0, 0);
            Grid.SetColumn(preview, 2);
            row.Children.Add(preview);
        }

        if (choice.Deletable)
        {
            var delete = new Button
            {
                Content = new PathIcon { Width = 12, Height = 12, Data = (Geometry)Application.Current!.FindResource("IconClose")! },
                Classes = { "remove" },
                Width = DeleteWidth,
                // As high as the row's words, so a row with the button is no taller than one without.
                Height = 24,
                Padding = new Thickness(0),
                VerticalAlignment = VerticalAlignment.Center,
            };
            string name = Strings.Format("Equaliser_DeleteNamed", choice.Name);
            AutomationProperties.SetName(delete, name);
            ToolTip.SetTip(delete, name);
            delete.Click += (_, e) =>
            {
                e.Handled = true;
                AskToDelete(tag);
            };
            Grid.SetColumn(delete, 3);
            row.Children.Add(delete);
        }

        return row;
    }

    /// <summary>The row of one about to be deleted: Delete and Cancel in its place.</summary>
    private StackPanel Confirm(Choice choice)
    {
        var delete = new Button { Content = Strings.Get("Equaliser_Delete") };
        var cancel = new Button { Content = Strings.Get("Dialog_Cancel") };
        // The keyboard lands on the safe choice, not the destructive one.
        cancel.AttachedToVisualTree += (_, _) => Dispatcher.UIThread.Post(() => cancel.Focus());
        AutomationProperties.SetName(delete, Strings.Format("Equaliser_DeleteNamed", choice.Name));
        AutomationProperties.SetName(cancel, Strings.Format("Equaliser_KeepNamed", choice.Name));
        delete.Click += (_, e) =>
        {
            e.Handled = true;
            _confirming = null;
            Deleted?.Invoke(choice.Tag!);
        };
        cancel.Click += (_, e) =>
        {
            e.Handled = true;
            _confirming = null;
            Fill();
        };
        return new StackPanel { Orientation = Orientation.Horizontal, Spacing = Space.S, Children = { delete, cancel } };
    }

    /// <summary>Turns a row into Delete and Cancel.</summary>
    /// <remarks>Posted, so the button pressed is not taken out of the list while its press is still being handled.</remarks>
    private void AskToDelete(object tag) => Dispatcher.UIThread.Post(() =>
    {
        _confirming = tag;
        Fill();
    });
}
