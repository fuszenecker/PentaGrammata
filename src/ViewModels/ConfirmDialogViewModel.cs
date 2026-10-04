using System;

using CommunityToolkit.Mvvm.Input;

namespace PentaGrammata.ViewModels;

public sealed class ConfirmDialogViewModel : ViewModelBase
{
    public string Title { get; }
    public string Message { get; }
    public string ConfirmButtonText { get; }

    public IRelayCommand ConfirmCommand { get; }
    public IRelayCommand CancelCommand { get; }

    /// <summary>Carries the user's decision; the dialog closes with it as its result.</summary>
    public event Action<bool>? CloseRequested;

    public ConfirmDialogViewModel(string title, string message, string confirmButtonText)
    {
        Title = title;
        Message = message;
        ConfirmButtonText = confirmButtonText;
        ConfirmCommand = new RelayCommand(() => CloseRequested?.Invoke(true));
        CancelCommand = new RelayCommand(() => CloseRequested?.Invoke(false));
    }
}
