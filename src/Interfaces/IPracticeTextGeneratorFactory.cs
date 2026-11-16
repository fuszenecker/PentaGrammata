using PentaGrammata.Configuration;

namespace PentaGrammata.Interfaces;

/// <summary>Returns the text generator of a practice topic, built from the configuration.</summary>
public interface IPracticeTextGeneratorFactory
{
    IPracticeTextGenerator Create(PracticeTopic topic, AppConfiguration configuration);
}
