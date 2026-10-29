using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Neap.Desktop.Controls;

/// <summary>One row of a <see cref="Chooser"/>'s list.</summary>
/// <param name="Name">What the row says, and its name for a screen reader.</param>
/// <param name="Tag">What choosing the row chooses; null for a heading.</param>
public sealed record ChooserItem(string Name, object? Tag)
{
    /// <summary>Drawn right after the name, such as a Default tag.</summary>
    public Control? Badge { get; init; }

    /// <summary>What a screen reader says of the row besides its name, such as what its badge says.</summary>
    public string? Status { get; init; }

    /// <summary>Drawn at the row's end, such as a preset's curve.</summary>
    public Control? Preview { get; init; }

    /// <summary>The row in use, shown selected.</summary>
    public bool Chosen { get; init; }

    /// <summary>The row can be deleted, with a delete button at its end.</summary>
    public bool Deletable { get; init; }

    /// <summary>A heading over the rows after it, which cannot be chosen.</summary>
    public static ChooserItem Heading(string name) => new(name, null);
}

/// <summary>
/// Neap's dropdown for choosing one of a list: a face that names what is
/// chosen, and under it the actions that make or change a choice, then the
/// list with the chosen row selected.
/// </summary>
/// <remarks>
/// <para>
/// It is the dropdown button itself, so a page places, sizes and names it as
/// any button, and its own chevron is the sign that it opens a list. Profiles,
/// the equaliser presets on Home and in the mini panel, the parametric presets
/// and the curves the parametric equaliser is drawn against all choose with it.
/// </para>
/// <para>
/// Every list follows the same rules. The face names the choice, with the
/// Edited tag once it has been changed since. The chosen row is selected,
/// edited or not, and choosing it again puts it back as it was. Doing without
/// is a row of its own, such as None, not an action. Above the list are only
/// the actions that make a new choice, such as Load or New profile.
/// </para>
/// <para>
/// A row the person made has the ten-band equaliser's delete button at its
/// end, shown when the row is pointed at or focused, and asks Delete or Cancel
/// in the row's place, with no dialog and the keyboard on Cancel. The Delete
/// key does the same on a focused row, since a button inside a list is not
/// reached with Tab.
/// </para>
/// <para>
/// The list stays as it was given until it is given again, so a caller fills
/// it when what it lists changes, not on every repaint.
/// </para>
/// </remarks>
public class Chooser : DropDownButton
{
    /// <summary>The width of a row's delete button, as on the ten-band equaliser's rows.</summary>
    private const double DeleteWidth = 32;

    // Spaced by margins rather than the grid's column spacing, which would leave a gap where there is no sample or tag.
    private readonly ContentControl _lead = new() { VerticalAlignment = VerticalAlignment.Center, IsVisible = false, Margin = new Thickness(0, 0, Space.S, 0) };
    private readonly TextBlock _text = new() { VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
    private readonly ContentControl _tag = new() { VerticalAlignment = VerticalAlignment.Center, IsVisible = false, Margin = new Thickness(Space.S, 0, 0, 0) };
    private readonly StackPanel _actions = new() { Classes = { "dialogflow" } };
    private readonly Border _actionsSection;
    private readonly ListBox _list = new();
    private readonly Flyout _flyout;
    private IReadOnlyList<ChooserItem> _items = [];
    private object? _confirming;
    private bool _filling;

    /// <param name="listName">The list's name for a screen reader.</param>
    public Chooser(string listName)
    {
        HorizontalContentAlignment = HorizontalAlignment.Stretch;
        Grid.SetColumn(_text, 1);
        Grid.SetColumn(_tag, 2);
        Content = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"),
            Children = { _lead, _text, _tag },
        };
        Flyout = _flyout = new Flyout { Placement = PlacementMode.BottomEdgeAlignedLeft };
        AutomationProperties.SetName(_list, listName);
        _actionsSection = new Border { Classes = { "underlined", "profileactions" }, Child = _actions, IsVisible = false };
        _flyout.Content = new StackPanel { Classes = { "profilemenu" }, Children = { _actionsSection, _list } };

        _list.SelectionChanged += (_, _) =>
        {
            if (!_filling && _list.SelectedItem is ListBoxItem { Tag: { } chosen }) Choose(chosen);
        };
        _list.KeyDown += (_, e) =>
        {
            if ((e.Source as Control)?.FindAncestorOfType<ListBoxItem>(includeSelf: true) is not { Tag: { } tag } row) return;
            if (e.Key == Key.Enter && row.IsSelected) Choose(tag);
            else if (e.Key == Key.Delete && _items.FirstOrDefault(i => Equals(i.Tag, tag)) is { Deletable: true }) AskToDelete(tag);
            else return;
            e.Handled = true;
        };
        _flyout.Closed += (_, _) =>
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

    /// <summary>Where the list opens against the face.</summary>
    public PlacementMode ListPlacement
    {
        get => _flyout.Placement;
        set => _flyout.Placement = value;
    }

    /// <summary>Whether a row can be chosen now, such as while nothing else is being switched.</summary>
    public bool ListEnabled
    {
        get => _list.IsEnabled;
        set => _list.IsEnabled = value;
    }

    /// <summary>Whether the face's words are the small size, as in the side menu.</summary>
    public bool SmallText
    {
        get => _text.Classes.Contains("caption");
        set => _text.Classes.Set("caption", value);
    }

    /// <summary>What the face says.</summary>
    public string? Text => _text.Text;

    protected override Type StyleKeyOverride => typeof(DropDownButton);

    /// <summary>Names what is chosen on the face.</summary>
    /// <param name="text">What the face says.</param>
    /// <param name="name">The face's name for a screen reader, which says what it chooses as well as what is chosen; or null to leave the name it has.</param>
    /// <param name="lead">Drawn before the words, such as a line's sample in a key; or null.</param>
    /// <param name="tag">Drawn after the words, such as an Edited tag; or null.</param>
    public void Show(string text, string? name = null, Control? lead = null, Control? tag = null)
    {
        _text.Text = text;
        if (name is not null) AutomationProperties.SetName(this, name);
        (_lead.Content, _lead.IsVisible) = (lead, lead is not null);
        (_tag.Content, _tag.IsVisible) = (tag, tag is not null);
    }

    /// <summary>Sets what sits above the list, such as a note and the actions that make or change a choice, or nothing.</summary>
    /// <remarks>A button there takes the list's width and closes the list as it is pressed, as choosing a row does.</remarks>
    public void Actions(params Control[] actions)
    {
        _actions.Children.Clear();
        foreach (var action in actions)
        {
            if (action is Button button)
            {
                button.HorizontalAlignment = HorizontalAlignment.Stretch;
                button.HorizontalContentAlignment = HorizontalAlignment.Left;
                button.Click += (_, _) => _flyout.Hide();
            }

            _actions.Children.Add(action);
        }

        _actionsSection.IsVisible = actions.Length > 0;
    }

    /// <summary>Lists the rows.</summary>
    public void List(IReadOnlyList<ChooserItem> items)
    {
        _items = items;
        Fill();
    }

    /// <summary>Closes the list.</summary>
    public void Close() => _flyout.Hide();

    /// <summary>Closes the list and says what was chosen.</summary>
    /// <remarks>
    /// Posted, as choosing usually lists the rows again, which cannot happen
    /// while the list is still selecting. The selection goes back to the row
    /// given as chosen first, so a row that only acts, such as a starting
    /// shape, is not left looking chosen when the rows are not listed again.
    /// </remarks>
    private void Choose(object chosen)
    {
        _flyout.Hide();
        Dispatcher.UIThread.Post(() =>
        {
            _filling = true;
            try { _list.SelectedIndex = _items.ToList().FindIndex(i => i.Chosen && i.Tag is not null); }
            finally { _filling = false; }
            Chosen?.Invoke(chosen);
        });
    }

    private void Fill()
    {
        _filling = true;
        try
        {
            _list.Items.Clear();
            ListBoxItem? chosen = null;
            foreach (var item in _items)
            {
                var row = item.Tag is null ? Heading(item.Name)
                    : Equals(item.Tag, _confirming) ? Confirm(item)
                    : Row(item);
                _list.Items.Add(row);
                if (item.Chosen && item.Tag is not null) chosen = row;
            }

            _list.SelectedItem = chosen;
            _list.IsVisible = _items.Count > 0;
        }
        finally { _filling = false; }
    }

    private static ListBoxItem Heading(string name) => new()
    {
        Content = new TextBlock { Text = name, Classes = { "label" } },
        IsEnabled = false,
        Focusable = false,
    };

    /// <summary>A row: its name and badge, its preview, and its delete button when it has one.</summary>
    private ListBoxItem Row(ChooserItem item)
    {
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto"), ColumnSpacing = Space.S, Classes = { "slot" } };
        var name = new TextBlock { Text = item.Name, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
        row.Children.Add(item.Badge is { } badge
            ? new StackPanel { Classes = { "gaprow" }, Children = { name, Moved(badge) } }
            : name);
        if (item.Preview is { } preview)
        {
            Moved(preview).Margin = new Thickness(Space.S, 0, 0, 0);
            Grid.SetColumn(preview, 1);
            row.Children.Add(preview);
        }

        if (item.Deletable)
        {
            var delete = new Button
            {
                Content = new PathIcon { Width = 12, Height = 12, Data = (Geometry)Application.Current!.FindResource("IconClose")! },
                Classes = { "remove" },
                Width = DeleteWidth,
            };
            string deleteName = Strings.Format("Equaliser_DeleteNamed", item.Name);
            AutomationProperties.SetName(delete, deleteName);
            ToolTip.SetTip(delete, deleteName);
            delete.Click += (_, _) => AskToDelete(item.Tag!);
            Grid.SetColumn(delete, 2);
            row.Children.Add(delete);
        }

        var listItem = new ListBoxItem { Content = row, Tag = item.Tag, Classes = { "preset" } };
        AutomationProperties.SetName(listItem, item.Name);

        // A press on the row already selected chooses it again, which selecting alone would not report.
        bool wasSelected = false;
        listItem.AddHandler(PointerPressedEvent, (_, _) => wasSelected = listItem.IsSelected, RoutingStrategies.Tunnel);
        listItem.Tapped += (_, e) =>
        {
            if (!wasSelected || e.Source is Control source && source.FindAncestorOfType<Button>(includeSelf: true) is not null) return;
            Choose(item.Tag!);
        };
        if (item.Status is { } status) AutomationProperties.SetItemStatus(listItem, status);
        return listItem;
    }

    /// <summary>A control given with a row, taken out of the row it was drawn in before, as the list is drawn again while it asks Delete or Cancel.</summary>
    private static Control Moved(Control control)
    {
        (control.Parent as Panel)?.Children.Remove(control);
        return control;
    }

    /// <summary>The row of one about to be deleted: Delete and Cancel in its place.</summary>
    private ListBoxItem Confirm(ChooserItem item)
    {
        var delete = new Button { Content = Strings.Get("Equaliser_Delete") };
        var cancel = new Button { Content = Strings.Get("Dialog_Cancel") };
        // The keyboard lands on the safe choice, not the destructive one.
        cancel.AttachedToVisualTree += (_, _) => Dispatcher.UIThread.Post(() => cancel.Focus());
        AutomationProperties.SetName(delete, Strings.Format("Equaliser_DeleteNamed", item.Name));
        AutomationProperties.SetName(cancel, Strings.Format("Equaliser_KeepNamed", item.Name));
        delete.Click += (_, _) =>
        {
            _confirming = null;
            Deleted?.Invoke(item.Tag!);
        };
        cancel.Click += (_, _) =>
        {
            _confirming = null;
            Fill();
        };
        return new ListBoxItem
        {
            Content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = Space.S, Children = { delete, cancel } },
            Classes = { "preset" },
            Focusable = false,
        };
    }

    /// <summary>Turns a row into Delete and Cancel.</summary>
    /// <remarks>Posted, so the button pressed is not taken out of the list while its press is still being handled.</remarks>
    private void AskToDelete(object tag) => Dispatcher.UIThread.Post(() =>
    {
        _confirming = tag;
        Fill();
    });
}
