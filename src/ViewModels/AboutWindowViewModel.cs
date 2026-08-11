using System.Reflection;
using CommunityToolkit.Mvvm.Input;

namespace PentaGrammata.ViewModels;

public sealed class AboutWindowViewModel : ViewModelBase
{
    public string VersionText { get; }

    public string CopyrightText { get; }

    public IRelayCommand CloseCommand { get; }

    public event System.Action? CloseRequested;

    public AboutWindowViewModel()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var version = assembly.GetName().Version?.ToString() ?? "unknown";
        VersionText = $"Version {version}";
        var copyright = assembly.GetCustomAttribute<AssemblyCopyrightAttribute>()?.Copyright;
        CopyrightText = string.IsNullOrWhiteSpace(copyright)
            ? "Copyright © 2026 Róbert Fuszenecker, HA8LHS"
            : copyright;
        CloseCommand = new RelayCommand(() => CloseRequested?.Invoke());
    }
}
