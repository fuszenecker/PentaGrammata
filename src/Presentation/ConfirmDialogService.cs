using System;
using System.Threading.Tasks;

using PentaGrammata.Interfaces;
using PentaGrammata.ViewModels;
using PentaGrammata.Views;

namespace PentaGrammata.Presentation;

public sealed class ConfirmDialogService : IConfirmDialogService
{
    private readonly IWindowContext _windowContext;
    private readonly IDialogViewModelFactory _viewModelFactory;

    public ConfirmDialogService(IWindowContext windowContext, IDialogViewModelFactory viewModelFactory)
    {
        _windowContext = windowContext ?? throw new ArgumentNullException(nameof(windowContext));
        _viewModelFactory = viewModelFactory ?? throw new ArgumentNullException(nameof(viewModelFactory));
    }

    public async Task<bool> ShowConfirmAsync(string title, string message, string confirmButtonText)
    {
        var owner = _windowContext.ActiveWindow;
        if (owner is null)
        {
            return false;
        }

        var viewModel = _viewModelFactory.CreateConfirm(title, message, confirmButtonText);
        return await new ConfirmDialog(viewModel).ShowDialog<bool>(owner);
    }
}
