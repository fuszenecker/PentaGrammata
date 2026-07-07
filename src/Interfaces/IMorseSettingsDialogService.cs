using System.Threading.Tasks;
using AppConfig = PentaGrammata.Configuration.AppConfiguration;

namespace PentaGrammata.Interfaces;

public interface IMorseSettingsDialogService
{
    Task<AppConfig?> ShowSettingsDialogAsync(AppConfig currentSettings);
}
