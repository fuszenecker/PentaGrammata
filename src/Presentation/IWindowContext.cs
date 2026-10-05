using Avalonia.Controls;

namespace PentaGrammata.Presentation;

public interface IWindowContext
{
    Window? MainWindow { get; set; }

    /// <summary>
    /// Returns the currently active (focused) window, falling back to
    /// <see cref="MainWindow"/> when no window reports focus.
    /// </summary>
    Window? ActiveWindow { get; }
}
