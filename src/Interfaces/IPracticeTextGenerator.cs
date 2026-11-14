namespace PentaGrammata.Interfaces;

/// <summary>Produces the text of one practice session.</summary>
public interface IPracticeTextGenerator
{
    /// <summary>
    /// Generates a session's text. <paramref name="groupCount"/> is the length the session
    /// duration is worth, in 5-character groups; word topics add items until they have sent
    /// that many characters. The custom-text topic ignores it.
    /// </summary>
    string Generate(int groupCount);
}
