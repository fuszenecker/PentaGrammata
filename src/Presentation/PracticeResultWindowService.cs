using System.Threading.Tasks;
using PentaGrammata.Configuration;
using PentaGrammata.Interfaces;
using PentaGrammata.Models;
using PentaGrammata.ViewModels;
using PentaGrammata.Views;

namespace PentaGrammata.Presentation;

public sealed class PracticeResultWindowService : IPracticeResultWindowService
{
    private readonly IWindowContext _windowContext;
    private readonly IDialogViewModelFactory _viewModelFactory;
    private readonly IWindowSizeService _windowSizeService;
    private readonly IInfoDialogService _infoDialogService;

    public PracticeResultWindowService(
        IWindowContext windowContext,
        IDialogViewModelFactory viewModelFactory,
        IWindowSizeService windowSizeService,
        IInfoDialogService infoDialogService)
    {
        _windowContext = windowContext;
        _viewModelFactory = viewModelFactory;
        _windowSizeService = windowSizeService;
        _infoDialogService = infoDialogService;
    }

    public async Task<bool> ShowPracticeResultAsync(
        PracticeResult result,
        int characterWpm,
        int averageWpm,
        bool alreadySaved,
        double errorThresholdPercent,
        NoiseSettings noise)
    {
        var owner = _windowContext.MainWindow;
        if (owner is null)
        {
            return false;
        }

        var viewModel = _viewModelFactory.CreatePracticeResult(
            result,
            characterWpm,
            averageWpm,
            alreadySaved,
            errorThresholdPercent,
            noise,
            _infoDialogService);
        var resultWindow = new PracticeResultWindow
        {
            DataContext = viewModel
        };

        _windowSizeService.Track(resultWindow);
        await resultWindow.ShowDialog(owner);
        return viewModel.IsSaveCompleted;
    }
}
