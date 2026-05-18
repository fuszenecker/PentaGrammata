using Avalonia.Controls;

namespace PentaGrammata.Views;

public partial class InfoDialog : Window
{
    public InfoDialog()
        : this("Information", string.Empty)
    {
    }

    public InfoDialog(string title, string message)
    {
        InitializeComponent();

        Title = title;
        var (primaryMessage, detailMessage) = SplitMessage(message);
        PrimaryMessageBlock.Text = primaryMessage;
        DetailsBox.Text = detailMessage;
        OkButton.Click += (_, _) => Close();
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