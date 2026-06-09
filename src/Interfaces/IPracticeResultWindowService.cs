using PentaGrammata.Models;

namespace PentaGrammata.Interfaces;

public interface IPracticeResultWindowService
{
    void ShowPracticeResult(PracticeResult result, int characterWpm, int averageWpm);
    void ResetSavedState();
}
