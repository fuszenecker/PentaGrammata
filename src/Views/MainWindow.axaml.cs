using System;
using Avalonia.Controls;
using PentaGrammata.ViewModels;
using Avalonia.Interactivity;

namespace PentaGrammata.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    protected override async void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        // async void is the event-handler boundary; the view model reports its own failures.
        if (DataContext is MainWindowViewModel viewModel)
        {
            await viewModel.ShowStartupNoticesAsync();
        }
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