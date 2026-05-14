using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PentaGrammata.Services;

public class MorsePlayer(IAudioPlayer audioPlayer) : IMorsePlayer
{
    private readonly IAudioPlayer _audioPlayer = audioPlayer;

    public async Task PlayMorseCodeAsync(string morseCode, int charWpm, int averageWpm, int sampleRate, int beepRampMs, CancellationToken cancellationToken)
    {
        var audioData = await Task.Run(
            () => GenerateAudioData(morseCode.ToLower(), charWpm, averageWpm, sampleRate, beepRampMs),
            cancellationToken);

        cancellationToken.ThrowIfCancellationRequested();
        await _audioPlayer.PlayAudioAsync(audioData, sampleRate, cancellationToken);
    }

    private static short[] GenerateBeep(int sampleRate, int durationMs, int beepRampMs)
    {
        int sampleCount = (sampleRate * durationMs) / 1000;
        int rampSamples = Math.Min((sampleRate * beepRampMs) / 1000, sampleCount / 2);
        var audioData = new short[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            double envelope = 1.0;

            if (i < rampSamples)
            {
                envelope = (double)i / rampSamples;
            }
            else if (i >= sampleCount - rampSamples)
            {
                envelope = (double)(sampleCount - 1 - i) / rampSamples;
            }

            audioData[i] = (short)(Math.Sin(2 * Math.PI * 440 * i / sampleRate) * short.MaxValue * envelope);
        }

        return audioData;
    }

    private static short[] GenerateSilence(int sampleRate, int durationMs)
    {
        int sampleCount = (sampleRate * durationMs) / 1000;
        return new short[sampleCount]; // 16-bit audio silence
    }

    private static short[] GenerateAudioData(string morseCode, int charWpm, int averageWpm, int sampleRate, int beepRampMs)
    {        
        if (averageWpm > charWpm)
        {
            averageWpm = charWpm;
        }

        // Placeholder implementation: generate a simple beep for each dot and dash
        var audioData = new List<short>();

        // Calculate timing based on WPM
        int ditLengthMs = 1200 / charWpm; 

        // Extra time in ms to slow down to text WPM
        int extraTimeMs = 60000 * (charWpm - averageWpm) / charWpm;

        // Calculate extra time per character based on text WPM (assuming 5 characters per word: PARIS)
        int totalCharsPerMinute = averageWpm * 5; 

        // Extra time to add after each character to achieve the desired text WPM
        int extraTimePerCharMs = (int)((double)extraTimeMs / totalCharsPerMinute);

        for (int i = 0; i < morseCode.Length; i++)
        {
            string morseString;
            char c = morseCode[i];
            
            if (c == '<')
            {
                int endIndex = morseCode.IndexOf('>', i);
                if (endIndex == -1)
                {
                    continue; // Invalid format, skip
                }
                
                morseString = morseCode.Substring(i, endIndex - i + 1);
                i = endIndex;
            }
            else
            {
                morseString = c.ToString();
            }

            string morseSymbols = CharToMorse(morseString);
            foreach (char symbol in morseSymbols)
            {
                if (symbol == '.')
                {
                    audioData.AddRange(GenerateBeep(sampleRate, ditLengthMs, beepRampMs));
                }
                else if (symbol == '-')
                {
                    audioData.AddRange(GenerateBeep(sampleRate, 3 * ditLengthMs, beepRampMs));
                }
                else if (symbol == ' ')
                {
                    audioData.AddRange(GenerateSilence(sampleRate, (7 - 1 - 2) * ditLengthMs)); // Space between symbols: 400ms silence
                }

                audioData.AddRange(GenerateSilence(sampleRate, ditLengthMs)); // Space between dots/dashes: 100ms silence
            }

            audioData.AddRange(GenerateSilence(sampleRate, (3 - 1) * ditLengthMs)); // Space between characters: 200ms silence
            
            // Farnsworth timing: add extra silence after each character to slow down the overall speed to text WPM
            audioData.AddRange(GenerateSilence(sampleRate, extraTimePerCharMs));
        }

        return [.. audioData];
    }

    private static string CharToMorse(string morseChar)
    {
        return morseChar switch
        {
            "a" => ".-",
            "b" => "-...",
            "c" => "-.-.",
            "d" => "-..",
            "e" => ".",
            "f" => "..-.",
            "g" => "--.",
            "h" => "....",
            "i" => "..",
            "j" => ".---",
            "k" => "-.-",
            "l" => ".-..",
            "m" => "--",
            "n" => "-.",
            "o" => "---",
            "p" => ".--.",
            "q" => "--.-",
            "r" => ".-.",
            "s" => "...",
            "t" => "-",
            "u" => "..-",
            "v" => "...-",
            "w" => ".--",
            "x" => "-..-",
            "y" => "-.--",
            "z" => "--..",
            "1" => ".----",
            "2" => "..---",
            "3" => "...--",
            "4" => "....-",
            "5" => ".....",
            "6" => "-....",
            "7" => "--...",
            "8" => "---..",
            "9" => "----.",
            "0" => "-----",
            " " => " ",
            "/" => "-..-.",
            "=" => "-...-",
            "?" => "..--..",
            "+" => ".-.-.",
            "<ar>" => ".-.-.",
            "<as>" => ".-...",
            "<bk>" => "-...-.-",
            "<bt>" => "-...-",
            "<kn>" => "-.--.",
            "<sk>" => "...-.-",
            _ => ""
        };
    }
}