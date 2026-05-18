using System.Threading.Tasks;

namespace PentaGrammata.Interfaces;

public interface IInfoDialogService
{
    Task ShowInfoAsync(string title, string message);
}