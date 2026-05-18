using System.Threading.Tasks;
using PentaGrammata.Interfaces;
using PentaGrammata.Views;

namespace PentaGrammata.Services;

public sealed class InfoDialogService : IInfoDialogService
{
    private readonly IWindowContext _windowContext;

    public InfoDialogService(IWindowContext windowContext)
    {
        _windowContext = windowContext;
    }

    public async Task ShowInfoAsync(string title, string message)
    {
        var dialog = new InfoDialog(title, message);
        var owner = _windowContext.MainWindow;

        if (owner is null)
        {
            dialog.Show();
            return;
        }

        await dialog.ShowDialog(owner);
    }
}