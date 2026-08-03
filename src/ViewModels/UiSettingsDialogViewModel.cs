using System;
using System.Collections.Generic;
using System.Linq;

using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using PentaGrammata.Configuration;

namespace PentaGrammata.ViewModels;

public partial class UiSettingsDialogViewModel : ViewModelBase
{
    private readonly List<string> _suppressedDialogs;

    [ObservableProperty]
    private string receivedTextFontFamily = string.Empty;

    [ObservableProperty]
    private double receivedTextFontSize;

    [ObservableProperty]
    private bool revealSentTextAfterPractice;

    [ObservableProperty]
    private bool useLowercaseLetters;

    public IReadOnlyList<string> AvailableFonts { get; }

    public IRelayCommand SaveCommand { get; }
    public IRelayCommand CancelCommand { get; }

    public event Action<bool>? CloseRequested;

    public UiSettingsDialogViewModel(UiPreferences prefs)
    {
        _suppressedDialogs = [.. prefs.SuppressedDialogs];
        ReceivedTextFontFamily = prefs.ReceivedTextFontFamily;
        ReceivedTextFontSize = prefs.ReceivedTextFontSize;
        RevealSentTextAfterPractice = prefs.RevealSentTextAfterPractice;
        UseLowercaseLetters = prefs.UseLowercaseLetters;

        AvailableFonts = [.. FontManager.Current.SystemFonts.Select(f => f.Name).OrderBy(n => n, StringComparer.OrdinalIgnoreCase)];

        SaveCommand = new RelayCommand(() => CloseRequested?.Invoke(true));
        CancelCommand = new RelayCommand(() => CloseRequested?.Invoke(false));
    }

    public UiPreferences BuildPreferences() => new UiPreferences
    {
        SuppressedDialogs = [.. _suppressedDialogs],
        ReceivedTextFontFamily = ReceivedTextFontFamily,
        ReceivedTextFontSize = ReceivedTextFontSize,
        RevealSentTextAfterPractice = RevealSentTextAfterPractice,
        UseLowercaseLetters = UseLowercaseLetters,
    };
}
