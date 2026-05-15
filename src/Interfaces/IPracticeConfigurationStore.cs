using System.Threading.Tasks;
using AppConfig = PentaGrammata.Configuration.Configuration;

namespace PentaGrammata.Interfaces;

public interface IPracticeConfigurationStore
{
    AppConfig Load();

    Task SaveAsync(AppConfig configuration);
}
