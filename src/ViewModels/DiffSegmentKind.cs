namespace PentaGrammata.ViewModels;

/// <summary>
/// Toolkit-neutral classification of a difference segment in a practice result.
/// The view layer maps each kind to a concrete color via a value converter.
/// </summary>
public enum DiffSegmentKind
{
    /// <summary>Matching or whitespace text (shown muted).</summary>
    Unchanged,

    /// <summary>Text present in the copy but not sent (an insertion).</summary>
    Inserted,

    /// <summary>Text sent but missing from the copy (a deletion).</summary>
    Deleted,

    /// <summary>A substituted / mismatched character.</summary>
    Substituted,
}
