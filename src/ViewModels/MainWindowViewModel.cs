namespace PentaGrammata.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    public string Greeting { get; } = "Welcome to Avalonia!";
    public string StatusText { get; } = "Ready";
}
