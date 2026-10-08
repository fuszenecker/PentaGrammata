using System.Threading.Tasks;
using System;
using PentaGrammata.Configuration;
using PentaGrammata.Models;

namespace PentaGrammata.Presentation;

public interface IPracticeResultWindowService
{
    Task<bool> ShowPracticeResultAsync(
        PracticeResult result,
        int characterWpm,
        int averageWpm,
        Guid sessionId,
        bool alreadySaved,
        double errorThresholdPercent,
        NoiseSettings noise);
}
