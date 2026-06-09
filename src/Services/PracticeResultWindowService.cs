using System.Threading.Tasks;
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

    public PracticeResultWindowService(
        IWindowContext windowContext,
        IPracticeResultStatisticsStore statisticsStore,
        IInfoDialogService infoDialogService)
    {
        _windowContext = windowContext;
        _statisticsStore = statisticsStore;
        _infoDialogService = infoDialogService;
    }

    public async Task<bool> ShowPracticeResultAsync(PracticeResult result, int characterWpm, int averageWpm, bool alreadySaved)
    {
        var owner = _windowContext.MainWindow;
        if (owner is null)
        {
            return false;
        }

        var viewModel = new PracticeResultWindowViewModel(result, characterWpm, averageWpm, alreadySaved, _statisticsStore, _infoDialogService);
        var resultWindow = new PracticeResultWindow
        {
            DataContext = viewModel
        };

        await resultWindow.ShowDialog(owner);
        return viewModel.IsSaveCompleted;
    }
}
