using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;

namespace Neap.Desktop.Controls;

/// <summary>
/// One of the mini panel's tiles: a setting's icon, name and state, pressed to
/// turn it on or off, with a list of its choices where it has more than one
/// way of being on.
/// </summary>
/// <remarks>
/// <para>
/// The tile only draws and reports. The control that owns it decides what a
/// press does and says what to show, so the tile's state is always the
/// setting's: a press that changes nothing leaves the tile as it was.
/// </para>
/// <para>
/// The list is a separate button in the tile's corner, so the tile and its
/// list are two stops for the keyboard and two names for a screen reader.
/// Its look is in Tiles.axaml.
/// </para>
/// </remarks>
public sealed class QuickTile : Grid
{
    private readonly PathIcon _icon = new() { Classes = { "quickicon" } };
    private readonly TextBlock _title = new() { Classes = { "quicktitle" } };
    private readonly TextBlock _value = new() { Classes = { "quickvalue" } };
    private readonly ToggleButton _face;
    private readonly Button _more;
    private readonly MenuFlyout _choices = new();

    public QuickTile()
    {
        _icon.SetValue(DockPanel.DockProperty, Dock.Top);
        _face = new ToggleButton
        {
            Classes = { "quicktile" },
            IsEnabled = false,
            Content = new DockPanel
            {
                Children =
                {
                    _icon,
                    new StackPanel { VerticalAlignment = VerticalAlignment.Bottom, Children = { _title, _value } },
                },
            },
        };
        _face.Click += (_, _) =>
        {
            // The owner says whether the press took; until then the tile shows the setting as it is.
            _face.IsChecked = !_face.IsChecked;
            Pressed?.Invoke();
        };
        _more = new Button
        {
            Classes = { "quiet", "quickmore" },
            IsVisible = false,
            Flyout = _choices,
            Content = new PathIcon { Data = (Geometry)Application.Current!.FindResource("IconChevronDown")! },
        };
        Children.Add(_face);
        Children.Add(_more);
    }

    /// <summary>The tile was pressed. The owner turns the setting on or off and shows the result.</summary>
    public event Action? Pressed;

    /// <summary>Sets the tile's icon.</summary>
    public Geometry Icon { set => _icon.Data = value; }

    /// <summary>Sets the short name on the tile, and the full names a screen reader says for it and for its list, where it has one.</summary>
    public void Describe(string shortName, string fullName, string? listName = null)
    {
        _title.Text = shortName;
        AutomationProperties.SetName(_face, fullName);
        AutomationProperties.SetName(_more, listName);
    }

    /// <summary>Shows whether the setting is on, the word for its state, and whether a press can change it.</summary>
    public void Show(bool on, string state, bool canPress)
    {
        _face.IsChecked = on;
        Classes.Set("on", on);
        _face.IsEnabled = canPress;
        _value.Text = state;
        AutomationProperties.SetHelpText(_face, state);
        ToolTip.SetTip(_face, state);
    }

    /// <summary>Offers the setting's choices in the tile's list, the current one ticked.</summary>
    /// <param name="choices">Each choice's word, whether it is the current one, and what choosing it does.</param>
    /// <param name="canChoose">Whether a choice can be made now.</param>
    /// <remarks>
    /// The same choices only move the tick. Choosing from the list changes the
    /// setting, which is offered again as the list closes, and items replaced
    /// then leave the list taking itself for open, so the next click anywhere
    /// in the panel only closes it.
    /// </remarks>
    public void Offer(IEnumerable<(string Word, bool Current, Action Choose)> choices, bool canChoose)
    {
        var offered = choices.ToList();
        if (offered.Select(c => c.Word).SequenceEqual(_choices.Items.OfType<MenuItem>().Select(i => i.Header as string)))
        {
            foreach (var (item, (_, current, _)) in _choices.Items.OfType<MenuItem>().Zip(offered)) item.IsChecked = current;
        }
        else
        {
            _choices.Items.Clear();
            foreach (var (word, current, choose) in offered)
            {
                var item = new MenuItem { Header = word, ToggleType = MenuItemToggleType.Radio, IsChecked = current };
                item.Click += (_, _) => choose();
                _choices.Items.Add(item);
            }
        }
        _more.IsVisible = _choices.Items.Count > 1;
        _more.IsEnabled = canChoose;
    }
}
