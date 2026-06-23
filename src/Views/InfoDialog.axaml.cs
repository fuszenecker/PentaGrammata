using Avalonia.Controls;
using PentaGrammata.ViewModels;

namespace PentaGrammata.Views;

public partial class InfoDialog : Window
{
    public InfoDialog()
        : this(new InfoDialogViewModel("Information", string.Empty, string.Empty, false))
    {
    }

    public InfoDialog(InfoDialogViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.CloseRequested += (_, _) => Close();
    }
}
