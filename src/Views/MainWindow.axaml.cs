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
        ReceivedTextBox.CaretIndex = 0;
    }

    private void OnStopClick(object? sender, RoutedEventArgs e)
    {
        ReceivedTextBox.Focus();
        ReceivedTextBox.CaretIndex = 0;
    }

    private void OnExitClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    private void OnUndoClick(object? sender, RoutedEventArgs e)
    {
        ReceivedTextBox.Undo();
    }

    private void OnRedoClick(object? sender, RoutedEventArgs e)
    {
        ReceivedTextBox.Redo();
    }

    private void OnCutClick(object? sender, RoutedEventArgs e)
    {
        ReceivedTextBox.Cut();
    }

    private void OnCopyClick(object? sender, RoutedEventArgs e)
    {
        ReceivedTextBox.Copy();
    }

    private void OnPasteClick(object? sender, RoutedEventArgs e)
    {
        ReceivedTextBox.Paste();
    }

    private void OnSelectAllClick(object? sender, RoutedEventArgs e)
    {
        ReceivedTextBox.Focus();
        ReceivedTextBox.SelectAll();
    }
}