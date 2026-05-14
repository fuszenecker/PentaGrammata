using Avalonia.Controls;
using Avalonia.Interactivity;

using PentaGrammata.ViewModels;

namespace PentaGrammata.Views;

public partial class SettingsDialog : Window
{
    public SettingsDialog()
    {
        InitializeComponent();
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        Close(false);
    }

    private void OnSaveClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is SettingsDialogViewModel vm && vm.TryBuildSettings(out _))
        {
            Close(true);
        }
    }
}
