using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace SC2CCM.Mac;

/// <summary>Zamiennik MessageBox z WinForms (Avalonia nie ma wbudowanego).</summary>
public sealed class MessageDialog : Window
{
    public const string AppTitle = "StarCraft II Custom Campaign Manager";

    private MessageDialog(string title, string message, bool yesNo)
    {
        Title = title;
        Width = 440;
        SizeToContent = SizeToContent.Height;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;

        var text = new TextBlock
        {
            Text = message,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 20),
        };

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Right,
        };

        if (yesNo)
        {
            var no = new Button { Content = "No", MinWidth = 80, IsCancel = true };
            no.Click += (_, _) => Close(false);
            var yes = new Button { Content = "Yes", MinWidth = 80, IsDefault = true };
            yes.Click += (_, _) => Close(true);
            buttons.Children.Add(no);
            buttons.Children.Add(yes);
        }
        else
        {
            var ok = new Button { Content = "OK", MinWidth = 80, IsDefault = true, IsCancel = true };
            ok.Click += (_, _) => Close(true);
            buttons.Children.Add(ok);
        }

        var root = new StackPanel { Margin = new Thickness(24) };
        root.Children.Add(text);
        root.Children.Add(buttons);
        Content = root;
    }

    public static Task<bool> AskAsync(Window owner, string message, string title = AppTitle) =>
        new MessageDialog(title, message, yesNo: true).ShowDialog<bool>(owner);

    public static Task InfoAsync(Window owner, string message, string title = AppTitle) =>
        new MessageDialog(title, message, yesNo: false).ShowDialog<bool>(owner);
}
