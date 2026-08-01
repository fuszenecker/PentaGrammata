using System.Threading.Tasks;

namespace PentaGrammata.Interfaces;

public interface IInfoDialogService
{
    /// <summary>
    /// Shows an informational dialog. The first line of <paramref name="message"/> is the
    /// primary text; any remaining lines are shown in a detail box, optionally under
    /// <paramref name="detailHeading"/>.
    /// </summary>
    Task ShowInfoAsync(string title, string message, string? dialogKey = null, string? detailHeading = null);
}