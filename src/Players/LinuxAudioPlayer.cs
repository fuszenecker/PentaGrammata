using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

using PentaGrammata.Interfaces;

namespace PentaGrammata.Players;

public class LinuxAudioPlayer : IAudioPlayer
{
    private const string PulseLib = "libpulse-simple.so.0";

    // PA_SAMPLE_S16LE = 3
    // PA_STREAM_PLAYBACK = 1

    [StructLayout(LayoutKind.Sequential)]
    private struct PaSampleSpec
    {
        public uint Format;    // PA_SAMPLE_S16LE = 3
        public uint Rate;
        public byte Channels;
    }

    [DllImport(PulseLib, CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr pa_simple_new(
        IntPtr server, string name, int dir, IntPtr dev,
        string streamName, ref PaSampleSpec ss,
        IntPtr map, IntPtr attr, out int error);

    [DllImport(PulseLib, CallingConvention = CallingConvention.Cdecl)]
    private static extern int pa_simple_write(IntPtr s, byte[] data, UIntPtr bytes, out int error);

    [DllImport(PulseLib, CallingConvention = CallingConvention.Cdecl)]
    private static extern int pa_simple_drain(IntPtr s, out int error);

    [DllImport(PulseLib, CallingConvention = CallingConvention.Cdecl)]
    private static extern void pa_simple_free(IntPtr s);

    [DllImport(PulseLib, CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr pa_strerror(int error);

    public Task PlayAudioAsync(short[] audioData, int sampleRate, CancellationToken cancellationToken)
    {
        return Task.Run(() =>
        {
            if (audioData == null || audioData.Length == 0)
            {
                return;
            }

            var bytes = new byte[audioData.Length * 2];
            Buffer.BlockCopy(audioData, 0, bytes, 0, bytes.Length);

            var spec = new PaSampleSpec
            {
                Format = 3, // PA_SAMPLE_S16LE
                Rate = (uint)sampleRate,
                Channels = 1,
            };

            IntPtr stream = pa_simple_new(
                IntPtr.Zero, "PentaGrammata", 1, IntPtr.Zero,
                "Morse Code", ref spec, IntPtr.Zero, IntPtr.Zero, out int err);

            if (stream == IntPtr.Zero)
            {
                System.Diagnostics.Debug.WriteLine($"PulseAudio/PipeWire open error: {Marshal.PtrToStringAnsi(pa_strerror(err))}");
                return;
            }

            try
            {
                int chunkSize = sampleRate / 10 * 2; // 100ms chunks in bytes
                int offset = 0;

                while (offset < bytes.Length && !cancellationToken.IsCancellationRequested)
                {
                    int size = Math.Min(chunkSize, bytes.Length - offset);
                    var chunk = new byte[size];
                    Array.Copy(bytes, offset, chunk, 0, size);

                    if (pa_simple_write(stream, chunk, (UIntPtr)size, out err) < 0)
                    {
                        System.Diagnostics.Debug.WriteLine($"PulseAudio/PipeWire write error: {Marshal.PtrToStringAnsi(pa_strerror(err))}");
                        break;
                    }

                    offset += size;
                }

                if (!cancellationToken.IsCancellationRequested)
                {
                    pa_simple_drain(stream, out _);
                }
                cancellationToken.ThrowIfCancellationRequested();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"PulseAudio/PipeWire audio error: {ex.Message}");
            }
            finally
            {
                pa_simple_free(stream);
            }
        }, cancellationToken);
    }
}

