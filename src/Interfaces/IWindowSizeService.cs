using Avalonia.Controls;

namespace PentaGrammata.Interfaces;

/// <summary>
/// Remembers window sizes across runs, persisted to a dedicated configuration file.
/// Only the size (width/height) is stored — never the position.
/// </summary>
public interface IWindowSizeService
{
    /// <summary>
    /// Applies the saved size to <paramref name="window"/> (if any) and arranges for its
    /// size to be saved when it closes. Non-resizable windows are ignored, so callers can
    /// track any window unconditionally. The window's type name is used as the storage key.
    /// </summary>
    void Track(Window window);
}
