using Avalonia.Controls;
using System.ComponentModel;
using PentaGrammata.ViewModels;

namespace PentaGrammata.Views;

public partial class PracticeResultWindow : Window
{
    public PracticeResultWindow()
    {
        InitializeComponent();
        Closing += OnClosing;
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (DataContext is PracticeResultWindowViewModel { IsSaving: true })
        {
            e.Cancel = true;
        }
    }
}
