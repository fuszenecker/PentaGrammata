using System.Threading.Tasks;
using AppConfig = PentaGrammata.Configuration.AppConfiguration;

namespace PentaGrammata.Presentation;

public interface IMorseSettingsDialogService
{
    Task<AppConfig?> ShowSettingsDialogAsync(AppConfig currentSettings);
}
