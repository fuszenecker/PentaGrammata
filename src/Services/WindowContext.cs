using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using PentaGrammata.Interfaces;

namespace PentaGrammata.Services;

public sealed class WindowContext : IWindowContext
{
    public Window? MainWindow { get; set; }

    public Window? ActiveWindow
    {
        get
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                return desktop.Windows.FirstOrDefault(w => w.IsActive)
                    ?? MainWindow
                    ?? desktop.MainWindow;
            }

            return MainWindow;
        }
    }
}
