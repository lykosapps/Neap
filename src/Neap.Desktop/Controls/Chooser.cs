using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
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
/// A row that can be deleted has the ten-band equaliser's delete button at its
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
public sealed class Chooser : UserControl
{
    /// <summary>The width of a row's delete button, as on the ten-band equaliser's rows.</summary>
    private const double DeleteWidth = 32;

    private readonly DropDownButton _face = new() { HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Stretch };
    private readonly ContentControl _lead = new() { VerticalAlignment = VerticalAlignment.Center, IsVisible = false };
    private readonly TextBlock _text = new() { VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
    private readonly ContentControl _tag = new() { VerticalAlignment = VerticalAlignment.Center, IsVisible = false };
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
        Grid.SetColumn(_text, 1);
        Grid.SetColumn(_tag, 2);
        _face.Content = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"),
            ColumnSpacing = Space.S,
            Children = { _lead, _text, _tag },
        };
        _face.Flyout = _flyout = new Flyout { Placement = PlacementMode.BottomEdgeAlignedLeft };
        AutomationProperties.SetName(_list, listName);
        _actionsSection = new Border { Classes = { "underlined", "profileactions" }, Child = _actions, IsVisible = false };
        _flyout.Content = new StackPanel { Classes = { "profilemenu" }, Children = { _actionsSection, _list } };

        _list.SelectionChanged += (_, _) =>
        {
            if (_filling || _list.SelectedItem is not ListBoxItem { Tag: { } chosen }) return;
            _flyout.Hide();
            // Posted, as choosing usually lists the rows again, which cannot happen while the list is still selecting.
            Dispatcher.UIThread.Post(() => Chosen?.Invoke(chosen));
        };
        _list.KeyDown += (_, e) =>
        {
            if (e.Key != Key.Delete || (e.Source as Control)?.FindAncestorOfType<ListBoxItem>(includeSelf: true) is not { Tag: { } tag }) return;
            if (_items.FirstOrDefault(i => Equals(i.Tag, tag)) is not { Deletable: true }) return;
            e.Handled = true;
            AskToDelete(tag);
        };
        _flyout.Closed += (_, _) =>
        {
            if (_confirming is null) return;
            _confirming = null;
            Fill();
        };

        Content = _face;
    }

    /// <summary>A row was chosen, with its tag.</summary>
    public event Action<object>? Chosen;

    /// <summary>A row's deletion was confirmed, with its tag.</summary>
    public event Action<object>? Deleted;

    /// <summary>Names what is chosen on the face.</summary>
    /// <param name="text">What the face says.</param>
    /// <param name="name">The face's name for a screen reader, which says what it chooses as well as what is chosen.</param>
    /// <param name="lead">Drawn before the words, such as a line's sample in a key; or null.</param>
    /// <param name="tag">Drawn after the words, such as an Edited tag; or null.</param>
    public void Show(string text, string name, Control? lead = null, Control? tag = null)
    {
        _text.Text = text;
        AutomationProperties.SetName(_face, name);
        (_lead.Content, _lead.IsVisible) = (lead, lead is not null);
        (_tag.Content, _tag.IsVisible) = (tag, tag is not null);
    }

    /// <summary>Sets the actions above the list, or none.</summary>
    /// <remarks>An action closes the list as it is pressed, as choosing a row does.</remarks>
    public void Actions(params Button[] actions)
    {
        _actions.Children.Clear();
        foreach (var action in actions)
        {
            action.HorizontalAlignment = HorizontalAlignment.Stretch;
            action.HorizontalContentAlignment = HorizontalAlignment.Left;
            action.Click += (_, _) => _flyout.Hide();
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
        }
        finally { _filling = false; }
    }

    private static ListBoxItem Heading(string name) => new()
    {
        Content = new TextBlock { Text = name, Classes = { "label" } },
        IsEnabled = false,
        Focusable = false,
    };

    /// <summary>A row: its name, its preview, and its delete button when it has one.</summary>
    private ListBoxItem Row(ChooserItem item)
    {
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto"), ColumnSpacing = Space.S, Classes = { "slot" } };
        row.Children.Add(new TextBlock { Text = item.Name, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis });
        if (item.Preview is { } preview)
        {
            // The list is drawn again while it asks Delete or Cancel, so the preview may still be in the row it was drawn in before.
            (preview.Parent as Panel)?.Children.Remove(preview);
            preview.Margin = new Thickness(Space.S, 0, 0, 0);
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
            string name = Strings.Format("Equaliser_DeleteNamed", item.Name);
            AutomationProperties.SetName(delete, name);
            ToolTip.SetTip(delete, name);
            delete.Click += (_, _) => AskToDelete(item.Tag!);
            Grid.SetColumn(delete, 2);
            row.Children.Add(delete);
        }

        var listItem = new ListBoxItem { Content = row, Tag = item.Tag, Classes = { "preset" } };
        AutomationProperties.SetName(listItem, item.Name);
        return listItem;
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
