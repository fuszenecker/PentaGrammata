using System.Threading.Tasks;
using PentaGrammata.Configuration;
using PentaGrammata.Interfaces;
using PentaGrammata.Models;
using PentaGrammata.ViewModels;
using PentaGrammata.Views;

namespace PentaGrammata.Services;

public sealed class PracticeResultWindowService : IPracticeResultWindowService
{
    private readonly IWindowContext _windowContext;
    private readonly IDialogViewModelFactory _viewModelFactory;
    private readonly IWindowSizeService _windowSizeService;

    public PracticeResultWindowService(
        IWindowContext windowContext,
        IDialogViewModelFactory viewModelFactory,
        IWindowSizeService windowSizeService)
    {
        _windowContext = windowContext;
        _viewModelFactory = viewModelFactory;
        _windowSizeService = windowSizeService;
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
            noise);
        var resultWindow = new PracticeResultWindow
        {
            DataContext = viewModel
        };

        _windowSizeService.Track(resultWindow);
        await resultWindow.ShowDialog(owner);
        return viewModel.IsSaveCompleted;
    }
}
