using System.Threading.Tasks;
using AppConfig = PentaGrammata.Configuration.Configuration;

namespace PentaGrammata.Interfaces;

public interface IConfigurationService
{
    AppConfig Current { get; }
    Task SaveAsync();
}
