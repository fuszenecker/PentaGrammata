using System.Threading.Tasks;

namespace PentaGrammata.Presentation;

public interface IConfirmDialogService
{
    /// <summary>
    /// Shows a confirmation dialog. Returns true when the user chose the confirm button;
    /// false when cancelled (or when no window is available to own the dialog).
    /// </summary>
    Task<bool> ShowConfirmAsync(string title, string message, string confirmButtonText);
}
