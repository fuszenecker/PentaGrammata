using System;
using CommunityToolkit.Mvvm.Input;

namespace PentaGrammata.ViewModels;

public sealed class InfoDialogViewModel : ViewModelBase
{
    public string Title { get; }
    public string PrimaryMessage { get; }
    public string DetailMessage { get; }
    public bool HasDetail => !string.IsNullOrEmpty(DetailMessage);
    public string DetailHeading { get; }
    public bool HasDetailHeading => HasDetail && !string.IsNullOrEmpty(DetailHeading);
    public bool HasDoNotShowAgain { get; }
    public bool DoNotShowAgain { get; private set; }

    public IRelayCommand OkCommand { get; }
    public IRelayCommand DoNotShowAgainCommand { get; }

    public event EventHandler? CloseRequested;

    public InfoDialogViewModel(string title, string primaryMessage, string detailMessage, bool hasDoNotShowAgain, string? detailHeading = null)
    {
        Title = title;
        PrimaryMessage = primaryMessage;
        DetailMessage = detailMessage;
        DetailHeading = detailHeading ?? string.Empty;
        HasDoNotShowAgain = hasDoNotShowAgain;
        OkCommand = new RelayCommand(() => CloseRequested?.Invoke(this, EventArgs.Empty));
        DoNotShowAgainCommand = new RelayCommand(() =>
        {
            DoNotShowAgain = true;
            CloseRequested?.Invoke(this, EventArgs.Empty);
        });
    }
}
