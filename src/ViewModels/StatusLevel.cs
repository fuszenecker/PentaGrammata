namespace PentaGrammata.ViewModels;

/// <summary>
/// Toolkit-neutral status classification used by view models to convey the
/// meaning of a piece of state. The mapping to concrete colors lives in the
/// view layer (a value converter), keeping view models free of UI-toolkit types.
/// </summary>
public enum StatusLevel
{
    Neutral,
    Info,
    Success,
    Error,
}
