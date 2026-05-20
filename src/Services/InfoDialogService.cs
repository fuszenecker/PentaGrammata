using System;
using System.Threading.Tasks;
using PentaGrammata.Interfaces;
using PentaGrammata.Views;

namespace PentaGrammata.Services;

public sealed class InfoDialogService : IInfoDialogService
{
    private readonly IWindowContext _windowContext;

    public InfoDialogService(IWindowContext windowContext)
    {
        _windowContext = windowContext ?? throw new ArgumentNullException(nameof(windowContext));
    }

    public async Task ShowInfoAsync(string title, string message)
    {
        var owner = _windowContext.ActiveWindow;
        if (owner is null)
        {
            return;
        }

        await new InfoDialog(title, message).ShowDialog(owner);
    }
}