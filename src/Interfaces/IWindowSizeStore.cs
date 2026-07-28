namespace PentaGrammata.Interfaces;

/// <summary>
/// Reads and writes remembered window sizes, keyed by an opaque string, to a dedicated
/// configuration file. Holds no UI-toolkit types so it can be unit-tested directly; the
/// window wiring lives in <see cref="IWindowSizeService"/>. Only the size (width/height)
/// is stored — never the position.
/// </summary>
public interface IWindowSizeStore
{
    /// <summary>
    /// Returns the saved size for <paramref name="key"/>, or <c>null</c> if nothing is
    /// stored (or the stored value is missing/unusable).
    /// </summary>
    (double Width, double Height)? TryGetSize(string key);

    /// <summary>
    /// Persists the size for <paramref name="key"/>. Non-finite or non-positive values are
    /// ignored. Failures are swallowed (logged) so storage problems never surface to the UI.
    /// </summary>
    void SaveSize(string key, double width, double height);
}
