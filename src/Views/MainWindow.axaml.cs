using Avalonia.Controls;
using Avalonia.Interactivity;

using PentaGrammata.ViewModels;

namespace PentaGrammata.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private async void OnMorseSettingsClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            await vm.OpenSettingsDialogAsync(this);
        }
    }

    private void OnPracticeClick(object? sender, RoutedEventArgs e)
    {
        ReceivedTextBox.Focus();
    }

    private void OnCheckResultClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            vm.OpenResultWindow(this);
        }
    }

    private async void OnAboutClick(object? sender, RoutedEventArgs e)
    {
        var aboutWindow = new AboutWindow();
        await aboutWindow.ShowDialog(this);
    }
}