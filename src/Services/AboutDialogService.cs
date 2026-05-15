using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using PentaGrammata.Interfaces;
using PentaGrammata.ViewModels;
using PentaGrammata.Views;

namespace PentaGrammata.Services;

public sealed class AboutDialogService : IAboutDialogService
{
    public async Task ShowAboutAsync()
    {
        var owner = GetOwnerWindow();
        if (owner is null)
        {
            return;
        }

        var aboutWindow = new AboutWindow
        {
            DataContext = new AboutWindowViewModel()
        };

        await aboutWindow.ShowDialog(owner);
    }

    private static Window? GetOwnerWindow()
    {
        return (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;
    }
}
