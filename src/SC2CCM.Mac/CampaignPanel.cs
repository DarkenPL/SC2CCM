using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using SC2CCM.Core;

namespace SC2CCM.Mac;

/// <summary>Jedna „ramka” kampanii – odpowiednik wolBox/hotsBox/lotvBox/ncoBox z FormMain.</summary>
public sealed class CampaignPanel : Border
{
    public Campaign Campaign { get; }
    public CampaignInfo Info { get; }

    public event EventHandler? SetClicked;
    public event EventHandler? DeleteClicked;
    public event EventHandler? RestoreClicked;

    private readonly TextBlock _title = Value();
    private readonly TextBlock _author = Value();
    private readonly TextBlock _version = Value();
    private readonly TextBlock _warning;
    private readonly ComboBox _mods;
    private readonly Button _set, _delete, _restore;

    public Mod? SelectedMod => _mods.SelectedItem as Mod;

    public CampaignPanel(CampaignInfo info, Color accent)
    {
        Info = info;
        Campaign = info.Campaign;

        CornerRadius = new CornerRadius(10);
        BorderThickness = new Thickness(1);
        BorderBrush = new SolidColorBrush(accent, 0.6);
        Background = new SolidColorBrush(accent, 0.10);
        Padding = new Thickness(14, 12);
        Margin = new Thickness(6);

        // --- nagłówek
        var swatch = new Border
        {
            Width = 12, Height = 12, CornerRadius = new CornerRadius(6),
            Background = new SolidColorBrush(accent),
            VerticalAlignment = VerticalAlignment.Center,
        };
        var header = new TextBlock
        {
            Text = info.DisplayName, FontSize = 16, FontWeight = FontWeight.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
        };
        _warning = new TextBlock
        {
            Text = "⚠", Foreground = Brushes.Orange, FontSize = 16, IsVisible = false,
            VerticalAlignment = VerticalAlignment.Center,
        };
        ToolTip.SetTip(_warning,
            "Warning: Active campaign may have missing or inconsistent files.\n" +
            "This is usually due to changing campaigns while in a mission or menu.\n" +
            "Please exit to the menu in SC2 and set an active mod to clear this.");
        var headerRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        headerRow.Children.Add(swatch);
        headerRow.Children.Add(header);
        headerRow.Children.Add(_warning);

        // --- aktywny mod
        var details = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*"),
            RowDefinitions = new RowDefinitions("Auto,Auto,Auto"),
            Margin = new Thickness(0, 10, 0, 0),
        };
        AddRow(details, 0, "Active mod", _title);
        AddRow(details, 1, "Author", _author);
        AddRow(details, 2, "Version", _version);
        _title.FontWeight = FontWeight.SemiBold;

        // --- wybór modu
        var selectLabel = new TextBlock
        {
            Text = $"Select {info.ShortName} mod", Opacity = 0.75, FontSize = 12,
            Margin = new Thickness(0, 12, 0, 4),
        };
        _mods = new ComboBox
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            PlaceholderText = "No custom campaigns installed",
        };
        _mods.SelectionChanged += (_, _) => UpdateButtons();

        // --- przyciski
        _set = MakeButton("Set Active", "Set to Active Campaign", () => SetClicked?.Invoke(this, EventArgs.Empty));
        _delete = MakeButton("Delete Mod", "Delete Mod Files", () => DeleteClicked?.Invoke(this, EventArgs.Empty));
        _restore = MakeButton("Restore Default", "Restore to Unmodified (Blizzard campaign)", () => RestoreClicked?.Invoke(this, EventArgs.Empty));
        _set.Classes.Add("accent");

        var buttons = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,*,*"),
            Margin = new Thickness(0, 10, 0, 0),
        };
        Grid.SetColumn(_set, 0);
        Grid.SetColumn(_delete, 1);
        Grid.SetColumn(_restore, 2);
        _set.Margin = new Thickness(0, 0, 4, 0);
        _delete.Margin = new Thickness(2, 0, 2, 0);
        _restore.Margin = new Thickness(4, 0, 0, 0);
        buttons.Children.Add(_set);
        buttons.Children.Add(_delete);
        buttons.Children.Add(_restore);

        var stack = new StackPanel();
        stack.Children.Add(headerRow);
        stack.Children.Add(details);
        stack.Children.Add(selectLabel);
        stack.Children.Add(_mods);
        stack.Children.Add(buttons);
        Child = stack;

        ShowActive(null);
        UpdateButtons();
    }

    private static TextBlock Value() => new()
    {
        TextTrimming = TextTrimming.CharacterEllipsis,
        VerticalAlignment = VerticalAlignment.Center,
    };

    private static void AddRow(Grid grid, int row, string label, TextBlock value)
    {
        var l = new TextBlock
        {
            Text = label, Opacity = 0.7, FontSize = 12,
            Margin = new Thickness(0, 2, 12, 2), VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetRow(l, row);
        Grid.SetRow(value, row);
        Grid.SetColumn(value, 1);
        grid.Children.Add(l);
        grid.Children.Add(value);
    }

    private static Button MakeButton(string text, string tip, Action onClick)
    {
        var b = new Button
        {
            Content = text,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            FontSize = 12,
        };
        ToolTip.SetTip(b, tip);
        b.Click += (_, _) => onClick();
        return b;
    }

    private void UpdateButtons()
    {
        var hasSelection = SelectedMod != null;
        _set.IsEnabled = hasSelection;
        _delete.IsEnabled = hasSelection;
    }

    public void SetMods(IReadOnlyList<Mod> mods)
    {
        var previous = SelectedMod?.RootFolder;
        _mods.ItemsSource = mods.ToList();
        _mods.SelectedItem = mods.FirstOrDefault(m => m.RootFolder == previous);
        UpdateButtons();
    }

    public void Select(Mod mod)
    {
        _mods.SelectedItem = (_mods.ItemsSource as IEnumerable<Mod>)?.FirstOrDefault(m => m.RootFolder == mod.RootFolder);
        UpdateButtons();
    }

    public void ShowActive(Mod? active)
    {
        _title.Text = active?.Title ?? "Default Campaign";
        _author.Text = active?.Author ?? "Blizzard";
        _version.Text = active?.Version ?? "N/A";
    }

    public bool ShowWarning
    {
        set => _warning.IsVisible = value;
    }
}
