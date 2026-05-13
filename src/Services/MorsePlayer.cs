using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PentaGrammata.Services;

public class MorsePlayer(IAudioPlayer audioPlayer) : IMorsePlayer
{
    private readonly IAudioPlayer _audioPlayer = audioPlayer;

    public async Task PlayMorseCodeAsync(string morseCode, int charWpm, int textWpm, int sampleRate, CancellationToken cancellationToken)
    {
        var audioData = GenerateAudioData(morseCode.ToLower(), charWpm, textWpm, sampleRate);
        await _audioPlayer.PlayAudioAsync(audioData, sampleRate, cancellationToken);
    }

    private static short[] GenerateBeep(int sampleRate, int durationMs)
    {
        int sampleCount = (sampleRate * durationMs) / 1000;
        var audioData = new short[sampleCount]; // 16-bit audio
        for (int i = 0; i < sampleCount; i++)
        {
            short sampleValue = (short)(Math.Sin(2 * Math.PI * 440 * i / sampleRate) * short.MaxValue);
            audioData[i] = sampleValue;
        }
        return audioData;
    }

    private static short[] GenerateSilence(int sampleRate, int durationMs)
    {
        int sampleCount = (sampleRate * durationMs) / 1000;
        return new short[sampleCount]; // 16-bit audio silence
    }

    private static short[] GenerateAudioData(string morseCode, int charWpm, int textWpm, int sampleRate)
    {        
        // Placeholder implementation: generate a simple beep for each dot and dash
        var audioData = new List<short>();

        int ditLengthMs = 1200 / charWpm; // Duration of a dot in milliseconds

        for (int i = 0; i < morseCode.Length; i++)
        {
            string morseString;
            char c = morseCode[i];
            
            if (c == '<')
            {
                int endIndex = morseCode.IndexOf('>', i);
                if (endIndex == -1)
                    continue; // Invalid format, skip
                
                morseString = morseCode.Substring(i, endIndex - i + 1);
                i = endIndex; // Move index to end of special sequence
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
                    audioData.AddRange(GenerateBeep(sampleRate, ditLengthMs)); // Dot: 100ms beep
                }
                else if (symbol == '-')
                {
                    audioData.AddRange(GenerateBeep(sampleRate, 3 * ditLengthMs)); // Dash: 300ms beep
                }
                else if (symbol == ' ')
                {
                    audioData.AddRange(GenerateSilence(sampleRate, (7 - 1 - 2) * ditLengthMs)); // Space between symbols: 400ms silence
                }

                audioData.AddRange(GenerateSilence(sampleRate, ditLengthMs)); // Space between dots/dashes: 100ms silence
            }

            audioData.AddRange(GenerateSilence(sampleRate, (3 - 1) * ditLengthMs)); // Space between characters: 200ms silence
        }

        return audioData.ToArray();
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