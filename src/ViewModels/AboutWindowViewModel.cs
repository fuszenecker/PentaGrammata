using System.Reflection;
using CommunityToolkit.Mvvm.Input;

namespace PentaGrammata.ViewModels;

public sealed class AboutWindowViewModel : ViewModelBase
{
    public string VersionText { get; }

    public IRelayCommand CloseCommand { get; }

    public event System.Action? CloseRequested;

    public AboutWindowViewModel()
    {
        var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "unknown";
        VersionText = $"Version {version}";
        CloseCommand = new RelayCommand(() => CloseRequested?.Invoke());
    }
}
