using System.Threading.Tasks;
using AppConfig = PentaGrammata.Configuration.Configuration;

namespace PentaGrammata.Interfaces;

public interface ISettingsDialogService
{
    Task<AppConfig?> ShowSettingsDialogAsync(AppConfig currentSettings);
}
