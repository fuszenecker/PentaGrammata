using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using PentaGrammata.Interfaces;
using PentaGrammata.Models;
using PentaGrammata.ViewModels;
using PentaGrammata.Views;

namespace PentaGrammata.Services;

public sealed class PracticeResultWindowService : IPracticeResultWindowService
{
    public void ShowPracticeResult(PracticeResult result)
    {
        var owner = GetOwnerWindow();
        var resultWindow = new PracticeResultWindow
        {
            DataContext = new PracticeResultWindowViewModel(result)
        };

        if (owner is null)
        {
            resultWindow.Show();
            return;
        }

        resultWindow.Show(owner);
    }

    private static Window? GetOwnerWindow()
    {
        return (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;
    }
}
