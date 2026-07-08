using System.Threading.Tasks;
using PentaGrammata.Interfaces;
using PentaGrammata.ViewModels;
using PentaGrammata.Views;

namespace PentaGrammata.Services;

public sealed class AboutDialogService : IAboutDialogService
{
    private readonly IWindowContext _windowContext;
    private readonly IDialogViewModelFactory _viewModelFactory;

    public AboutDialogService(IWindowContext windowContext, IDialogViewModelFactory viewModelFactory)
    {
        _windowContext = windowContext;
        _viewModelFactory = viewModelFactory;
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
            DataContext = _viewModelFactory.CreateAbout()
        };

        await aboutWindow.ShowDialog(owner);
    }
}
