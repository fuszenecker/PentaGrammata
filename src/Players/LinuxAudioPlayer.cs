using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

using PentaGrammata.Exceptions;
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
    private static extern int pa_simple_flush(IntPtr s, out int error);

    [DllImport(PulseLib, CallingConvention = CallingConvention.Cdecl)]
    private static extern void pa_simple_free(IntPtr s);

    [DllImport(PulseLib, CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr pa_strerror(int error);

    public Task PlayAudioAsync(short[] audioData, int sampleRate, CancellationToken cancellationToken)
    {
        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
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
                throw CreateAudioException("opening the stream", err);
            }

            try
            {
                int chunkSize = Math.Max(sampleRate / 10 * 2, 2); // about 100ms in bytes
                int offset = 0;

                while (offset < bytes.Length)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    int size = Math.Min(chunkSize, bytes.Length - offset);
                    var chunk = new byte[size];
                    Array.Copy(bytes, offset, chunk, 0, size);

                    if (pa_simple_write(stream, chunk, (UIntPtr)size, out err) < 0)
                    {
                        throw CreateAudioException("writing audio", err);
                    }

                    offset += size;
                }

                if (cancellationToken.IsCancellationRequested)
                {
                    if (pa_simple_flush(stream, out err) < 0)
                    {
                        throw CreateAudioException("flushing cancelled audio", err);
                    }

                    cancellationToken.ThrowIfCancellationRequested();
                }
                else if (pa_simple_drain(stream, out err) < 0)
                {
                    throw CreateAudioException("draining audio", err);
                }
            }
            catch (OperationCanceledException)
            {
                if (pa_simple_flush(stream, out err) < 0)
                {
                    throw CreateAudioException("flushing cancelled audio", err);
                }

                throw;
            }
            finally
            {
                pa_simple_free(stream);
            }
        }, cancellationToken);
    }

    private static AudioPlaybackException CreateAudioException(string operation, int error)
    {
        var messagePointer = pa_strerror(error);
        var message = messagePointer == IntPtr.Zero ? null : Marshal.PtrToStringAnsi(messagePointer);
        return new AudioPlaybackException("PulseAudio/PipeWire", operation, error, message);
    }
}
