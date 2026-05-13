namespace PentaGrammata.Services;

public interface IMorsePlayer
{
    void PlayMorseCode(string morseCode, int charWpm, int textWpm, int sampleRate);
}