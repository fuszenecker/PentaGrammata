using System;

using Avalonia.Controls;

using PentaGrammata.Interfaces;

namespace PentaGrammata.Services;

/// <summary>
/// Wires a window up to <see cref="IWindowSizeStore"/>: applies the saved size before the
/// window is shown and saves the current size when it closes. All storage lives in the
/// store; this adapter only bridges the Avalonia <see cref="Window"/> to it.
/// </summary>
public sealed class WindowSizeService : IWindowSizeService
{
    private readonly IWindowSizeStore _store;

    public WindowSizeService(IWindowSizeStore store)
    {
        _store = store;
    }

    public void Track(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);

        if (!window.CanResize)
        {
            return;
        }

        var key = window.GetType().Name;

        if (_store.TryGetSize(key) is { } size)
        {
            window.Width = size.Width;
            window.Height = size.Height;
        }

        // Closing fires while Width/Height still reflect the user's final size.
        window.Closing += (_, _) => _store.SaveSize(key, window.Width, window.Height);
    }
}
