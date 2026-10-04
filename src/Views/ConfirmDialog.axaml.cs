using Avalonia.Controls;

using PentaGrammata.ViewModels;

namespace PentaGrammata.Views;

public partial class ConfirmDialog : Window
{
    public ConfirmDialog()
        : this(new ConfirmDialogViewModel("Confirm", string.Empty, "OK"))
    {
    }

    public ConfirmDialog(ConfirmDialogViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.CloseRequested += confirmed => Close(confirmed);
    }
}
