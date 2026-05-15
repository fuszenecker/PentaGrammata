using Avalonia.Controls;
using Avalonia.Interactivity;

namespace PentaGrammata.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private void OnPracticeClick(object? sender, RoutedEventArgs e)
    {
        ReceivedTextBox.Focus();
    }
}