using System.Reflection;

using Avalonia.Controls;
using Avalonia.Interactivity;

namespace PentaGrammata.Views;

public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();

        var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "unknown";
        VersionText.Text = $"Version {version}";
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
