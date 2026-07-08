using System;
using Avalonia.Controls;

using PentaGrammata.ViewModels;

namespace PentaGrammata.Views;

public partial class MorseSettingsDialog : Window
{
    private MorseSettingsDialogViewModel? _viewModel;

    public MorseSettingsDialog()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_viewModel is not null)
        {
            _viewModel.CloseRequested -= OnCloseRequested;
        }

        _viewModel = DataContext as MorseSettingsDialogViewModel;

        if (_viewModel is not null)
        {
            _viewModel.CloseRequested += OnCloseRequested;
        }
    }

    private void OnCloseRequested(bool saved)
    {
        Close(saved);
    }

    protected override void OnClosed(EventArgs e)
    {
        if (_viewModel is not null)
        {
            _viewModel.CloseRequested -= OnCloseRequested;
        }

        base.OnClosed(e);
    }
}
