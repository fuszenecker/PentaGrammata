using System.Threading.Tasks;
using PentaGrammata.Configuration;

namespace PentaGrammata.Interfaces;

public interface IUiSettingsDialogService
{
    Task<UiPreferences?> ShowUiSettingsDialogAsync(UiPreferences current);
}
