using System.Threading.Tasks;
using PentaGrammata.Configuration;

namespace PentaGrammata.Presentation;

public interface IUiSettingsDialogService
{
    Task<UiPreferences?> ShowUiSettingsDialogAsync(UiPreferences current);
}
