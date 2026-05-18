using PentaGrammata.Interfaces;
using PentaGrammata.Models;
using PentaGrammata.ViewModels;
using PentaGrammata.Views;

namespace PentaGrammata.Services;

public sealed class PracticeResultWindowService : IPracticeResultWindowService
{
    private readonly IWindowContext _windowContext;
    private readonly IPracticeResultStatisticsStore _statisticsStore;

    public PracticeResultWindowService(IWindowContext windowContext, IPracticeResultStatisticsStore statisticsStore)
    {
        _windowContext = windowContext;
        _statisticsStore = statisticsStore;
    }

    public void ShowPracticeResult(PracticeResult result, int characterWpm, int averageWpm)
    {
        var owner = _windowContext.MainWindow;
        var resultWindow = new PracticeResultWindow
        {
            DataContext = new PracticeResultWindowViewModel(result, characterWpm, averageWpm, _statisticsStore)
        };

        if (owner is null)
        {
            resultWindow.Show();
            return;
        }

        resultWindow.Show(owner);
    }
}
