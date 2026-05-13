using System;
using System.Collections.Generic;
using Silk.NET.OpenAL;

namespace PentaGrammata.Services;

public class AudioPlayer : IAudioPlayer, IDisposable
{
    private AL _al;
    private ALContext _alc;
    private unsafe Device* _device;
    private unsafe Context* _context;

    public unsafe AudioPlayer()
    {
        _alc = ALContext.GetApi();
        _al = AL.GetApi();
        
        _device = _alc.OpenDevice(null);
        if (_device == null)
            throw new Exception("Failed to open audio device");
        
        _context = _alc.CreateContext(_device, null);
        if (_context == null)
            throw new Exception("Failed to create OpenAL context");
        
        _alc.MakeContextCurrent(_context);
    }

    public unsafe void PlayAudio(short[] audioData, int sampleRate)
    {
        try
        {
            if (audioData == null || audioData.Length == 0)
                return;

            // Create audio buffer
            uint buffer = _al.GenBuffer();
            
            // Upload audio data to buffer
            fixed (short* ptr = audioData)
            {
                _al.BufferData(buffer, BufferFormat.Mono16, ptr, audioData.Length * sizeof(short), sampleRate);
            }

            // Create source and attach buffer
            uint source = _al.GenSource();
            _al.SetSourceProperty(source, SourceInteger.Buffer, (int)buffer);

            // Play the audio
            _al.SourcePlay(source);

            // Wait for playback to finish
            SourceState state = SourceState.Playing;
            while (state != SourceState.Stopped)
            {
                _al.GetSourceProperty(source, GetSourceInteger.SourceState, (int*)&state);
                System.Threading.Thread.Sleep(10);
            }

            // Cleanup
            _al.DeleteSource(source);
            _al.DeleteBuffer(buffer);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error playing audio: {ex.Message}");
        }
    }

    public unsafe void Dispose()
    {
        if (_context != null)
        {
            _alc.MakeContextCurrent(null);
            _alc.DestroyContext(_context);
        }
        
        if (_device != null)
        {
            _alc.CloseDevice(_device);
        }
    }
}