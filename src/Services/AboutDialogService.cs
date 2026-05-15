using System.Threading.Tasks;
using PentaGrammata.Interfaces;
using PentaGrammata.ViewModels;
using PentaGrammata.Views;

namespace PentaGrammata.Services;

public sealed class AboutDialogService : IAboutDialogService
{
    private readonly IWindowContext _windowContext;

    public AboutDialogService(IWindowContext windowContext)
    {
        _windowContext = windowContext;
    }

    public async Task ShowAboutAsync()
    {
        var owner = _windowContext.MainWindow;
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
}
