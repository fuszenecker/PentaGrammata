using System;
using System.Threading.Tasks;
using PentaGrammata.Interfaces;
using PentaGrammata.ViewModels;
using PentaGrammata.Views;

namespace PentaGrammata.Services;

public sealed class InfoDialogService : IInfoDialogService
{
    private readonly IWindowContext _windowContext;
    private readonly IConfigurationService _configurationService;

    public InfoDialogService(IWindowContext windowContext, IConfigurationService configurationService)
    {
        _windowContext = windowContext ?? throw new ArgumentNullException(nameof(windowContext));
        _configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));
    }

    public async Task ShowInfoAsync(string title, string message, string? dialogKey = null)
    {
        if (dialogKey is not null && _configurationService.IsDialogSuppressed(dialogKey))
        {
            return;
        }

        var owner = _windowContext.ActiveWindow;
        if (owner is null)
        {
            return;
        }

        var (primaryMessage, detailMessage) = SplitMessage(message);
        var viewModel = new InfoDialogViewModel(title, primaryMessage, detailMessage, dialogKey is not null);
        await new InfoDialog(viewModel).ShowDialog(owner);

        if (viewModel.DoNotShowAgain && dialogKey is not null)
        {
            await _configurationService.SuppressDialogAsync(dialogKey).ConfigureAwait(false);
        }
    }

    private static (string PrimaryMessage, string DetailMessage) SplitMessage(string message)
    {
        var separatorIndex = message.IndexOf('\n');
        if (separatorIndex < 0)
        {
            return (message, string.Empty);
        }

        var primaryMessage = message[..separatorIndex].TrimEnd();
        var detailMessage = message[(separatorIndex + 1)..].Trim();
        return (primaryMessage, detailMessage);
    }
}