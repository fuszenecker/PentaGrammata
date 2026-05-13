using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

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

    public async Task PlayAudioAsync(short[] audioData, int sampleRate, CancellationToken cancellationToken)
    {
        try
        {
            if (audioData == null || audioData.Length == 0)
                return;

            uint buffer = 0;
            uint source = 0;
            
            // Setup buffer and source (unsafe operations)
            unsafe
            {
                buffer = _al.GenBuffer();
                
                // Upload audio data to buffer
                fixed (short* ptr = audioData)
                {
                    _al.BufferData(buffer, BufferFormat.Mono16, ptr, audioData.Length * sizeof(short), sampleRate);
                }

                // Create source and attach buffer
                source = _al.GenSource();
                _al.SetSourceProperty(source, SourceInteger.Buffer, (int)buffer);

                // Play the audio
                _al.SourcePlay(source);
            }

            // Wait for playback to finish (outside unsafe context so we can await)
            while (true)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    unsafe
                    {
                        _al.SourceStop(source);
                    }
                    break;
                }

                bool isPlaying = GetSourceState(source);
                if (!isPlaying)
                    break;

                await Task.Delay(10, cancellationToken);
            }

            // Cleanup
            unsafe
            {
                _al.DeleteSource(source);
                _al.DeleteBuffer(buffer);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error playing audio: {ex.Message}");
        }
    }

    private unsafe bool GetSourceState(uint source)
    {
        int state = (int)SourceState.Stopped;
        _al.GetSourceProperty(source, GetSourceInteger.SourceState, &state);
        return state == (int)SourceState.Playing;
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