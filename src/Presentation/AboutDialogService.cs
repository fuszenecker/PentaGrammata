using System.Threading.Tasks;
using PentaGrammata.Interfaces;
using PentaGrammata.ViewModels;
using PentaGrammata.Views;

namespace PentaGrammata.Presentation;

public sealed class AboutDialogService : IAboutDialogService
{
    private readonly IWindowContext _windowContext;
    private readonly IDialogViewModelFactory _viewModelFactory;
    private readonly IWindowSizeService _windowSizeService;

    public AboutDialogService(IWindowContext windowContext, IDialogViewModelFactory viewModelFactory, IWindowSizeService windowSizeService)
    {
        _windowContext = windowContext;
        _viewModelFactory = viewModelFactory;
        _windowSizeService = windowSizeService;
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

        _windowSizeService.Track(aboutWindow);
        await aboutWindow.ShowDialog(owner);
    }
}
