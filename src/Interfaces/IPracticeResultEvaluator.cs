using PentaGrammata.Models;

namespace PentaGrammata.Interfaces;

public interface IPracticeResultEvaluator
{
    PracticeResult Evaluate(string sentText, string receivedText, double errorThresholdPercent);
}
