using AppConfig = PentaGrammata.Configuration.Configuration;

namespace PentaGrammata.Interfaces;

public interface IPracticeSettingsValidator
{
    bool TryValidate(AppConfig settings, out string error);
}
