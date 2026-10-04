using System;
using System.Threading.Tasks;

using PentaGrammata.Interfaces;
using PentaGrammata.ViewModels;
using PentaGrammata.Views;

namespace PentaGrammata.Presentation;

public sealed class ConfirmDialogService : IConfirmDialogService
{
    private readonly IWindowContext _windowContext;

    public ConfirmDialogService(IWindowContext windowContext)
    {
        _windowContext = windowContext ?? throw new ArgumentNullException(nameof(windowContext));
    }

    public async Task<bool> ShowConfirmAsync(string title, string message, string confirmButtonText)
    {
        var owner = _windowContext.ActiveWindow;
        if (owner is null)
        {
            return false;
        }

        var viewModel = new ConfirmDialogViewModel(title, message, confirmButtonText);
        return await new ConfirmDialog(viewModel).ShowDialog<bool>(owner);
    }
}
