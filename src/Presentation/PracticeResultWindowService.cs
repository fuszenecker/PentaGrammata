using System.Threading.Tasks;
using System;
using PentaGrammata.ViewModels;
using PentaGrammata.Configuration;
using PentaGrammata.Interfaces;
using PentaGrammata.Models;
using PentaGrammata.Views;

namespace PentaGrammata.Presentation;

public sealed class PracticeResultWindowService : IPracticeResultWindowService
{
    private readonly IWindowContext _windowContext;
    private readonly IDialogViewModelFactory _viewModelFactory;
    private readonly IWindowSizeService _windowSizeService;
    private readonly IInfoDialogService _infoDialogService;
    private Guid? _cachedSessionId;
    private PracticeResultWindowViewModel? _cachedViewModel;

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
        Guid sessionId,
        bool alreadySaved,
        double errorThresholdPercent,
        NoiseSettings noise)
    {
        var owner = _windowContext.MainWindow;
        if (owner is null)
        {
            return false;
        }

        if (_cachedSessionId != sessionId || _cachedViewModel is null)
        {
            _cachedSessionId = sessionId;
            _cachedViewModel = _viewModelFactory.CreatePracticeResult(
                result,
                characterWpm,
                averageWpm,
                sessionId,
                alreadySaved,
                errorThresholdPercent,
                noise,
                _infoDialogService);
        }

        // Keep the frozen record and save state beyond the dialog window's lifetime. A
        // reopen of the same session therefore retries the same record and session ID.
        var viewModel = _cachedViewModel;
        var resultWindow = new PracticeResultWindow
        {
            DataContext = viewModel
        };

        _windowSizeService.Track(resultWindow);
        await resultWindow.ShowDialog(owner);
        return viewModel.IsSaveCompleted;
    }
}
