using System.Threading.Tasks;
using PentaGrammata.Configuration;
using PentaGrammata.Models;

namespace PentaGrammata.Interfaces;

public interface IPracticeResultWindowService
{
    Task<bool> ShowPracticeResultAsync(PracticeResult result, int characterWpm, int averageWpm, bool alreadySaved, NoiseSettings noise);
}
