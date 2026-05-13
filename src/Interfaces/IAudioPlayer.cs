namespace PentaGrammata.Services;

public interface IAudioPlayer
{
    void PlayAudio(short[] audioData, int sampleRate);
}