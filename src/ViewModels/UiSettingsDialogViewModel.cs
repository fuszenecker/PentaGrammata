using System;
using System.Collections.Generic;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using PentaGrammata.Configuration;

namespace PentaGrammata.ViewModels;

public partial class UiSettingsDialogViewModel : ViewModelBase
{
    private readonly List<string> _suppressedDialogs;

    [ObservableProperty]
    private double receivedTextFontSize;

    [ObservableProperty]
    private bool revealSentTextAfterPractice;

    public IRelayCommand SaveCommand { get; }
    public IRelayCommand CancelCommand { get; }

    public event Action<bool>? CloseRequested;

    public UiSettingsDialogViewModel(UiPreferences prefs)
    {
        _suppressedDialogs = [.. prefs.SuppressedDialogs];
        ReceivedTextFontSize = prefs.ReceivedTextFontSize;
        RevealSentTextAfterPractice = prefs.RevealSentTextAfterPractice;

        SaveCommand = new RelayCommand(() => CloseRequested?.Invoke(true));
        CancelCommand = new RelayCommand(() => CloseRequested?.Invoke(false));
    }

    public UiPreferences BuildPreferences() => new UiPreferences
    {
        SuppressedDialogs = [.. _suppressedDialogs],
        ReceivedTextFontSize = ReceivedTextFontSize,
        RevealSentTextAfterPractice = RevealSentTextAfterPractice,
    };
}
