using PentaGrammata.Interfaces;
using PentaGrammata.Models;
using PentaGrammata.ViewModels;
using PentaGrammata.Views;

namespace PentaGrammata.Services;

public sealed class PracticeResultWindowService : IPracticeResultWindowService
{
    private readonly IWindowContext _windowContext;
    private readonly IPracticeResultStatisticsStore _statisticsStore;
    private readonly IInfoDialogService _infoDialogService;
    private PracticeResultWindowViewModel? _currentViewModel;

    public PracticeResultWindowService(
        IWindowContext windowContext,
        IPracticeResultStatisticsStore statisticsStore,
        IInfoDialogService infoDialogService)
    {
        _windowContext = windowContext;
        _statisticsStore = statisticsStore;
        _infoDialogService = infoDialogService;
    }

    public void ShowPracticeResult(PracticeResult result, int characterWpm, int averageWpm)
    {
        _currentViewModel ??= new PracticeResultWindowViewModel(result, characterWpm, averageWpm, _statisticsStore, _infoDialogService);

        var owner = _windowContext.MainWindow;
        var resultWindow = new PracticeResultWindow
        {
            DataContext = _currentViewModel
        };

        if (owner is null)
        {
            resultWindow.Show();
            return;
        }

        resultWindow.Show(owner);
    }

    public void ResetSavedState()
    {
        _currentViewModel = null;
    }
}
