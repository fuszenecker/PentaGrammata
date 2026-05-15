using PentaGrammata.Interfaces;
using PentaGrammata.Models;
using PentaGrammata.ViewModels;
using PentaGrammata.Views;

namespace PentaGrammata.Services;

public sealed class PracticeResultWindowService : IPracticeResultWindowService
{
    private readonly IWindowContext _windowContext;

    public PracticeResultWindowService(IWindowContext windowContext)
    {
        _windowContext = windowContext;
    }

    public void ShowPracticeResult(PracticeResult result)
    {
        var owner = _windowContext.MainWindow;
        var resultWindow = new PracticeResultWindow
        {
            DataContext = new PracticeResultWindowViewModel(result)
        };

        if (owner is null)
        {
            resultWindow.Show();
            return;
        }

        resultWindow.Show(owner);
    }
}
