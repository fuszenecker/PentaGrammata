using System;

using Avalonia.Controls;
using Avalonia.Threading;

using PentaGrammata.ViewModels;

namespace PentaGrammata.Views;

public partial class ConfusionsDialog : Window
{
    private ConfusionsDialogViewModel? _viewModel;

    public ConfusionsDialog()
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

        _viewModel = DataContext as ConfusionsDialogViewModel;
        if (_viewModel is not null)
        {
            _viewModel.CloseRequested += OnCloseRequested;
        }
    }

    private void OnCloseRequested()
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            Close();
            return;
        }

        Dispatcher.UIThread.Post(Close);
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
