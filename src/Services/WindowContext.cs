using Avalonia.Controls;
using PentaGrammata.Interfaces;

namespace PentaGrammata.Services;

public sealed class WindowContext : IWindowContext
{
    public Window? MainWindow { get; set; }
}
