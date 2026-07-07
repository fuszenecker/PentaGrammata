using AppConfig = PentaGrammata.Configuration.AppConfiguration;

namespace PentaGrammata.Interfaces;

public interface IPracticeSettingsValidator
{
    bool TryValidate(AppConfig settings, out string error);
}
