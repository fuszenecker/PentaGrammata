using System;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

using PentaGrammata.Exceptions;
using PentaGrammata.Interfaces;

namespace PentaGrammata.Players;

/// <summary>Plays audio on macOS through an AudioToolbox output queue (AudioQueue).</summary>
public class MacOSAudioPlayer : IAudioPlayer
{
    private const string AudioToolbox = "/System/Library/Frameworks/AudioToolbox.framework/AudioToolbox";
    private const string Backend = "macOS AudioToolbox";

    private const uint FormatLinearPcm = 0x6C70636D;          // 'lpcm'
    private const uint FormatFlagSignedIntegerPacked = 4 | 8; // kLinearPCMFormatFlagIsSignedInteger | IsPacked
    private const uint PropertyIsRunning = 0x6171726E;        // 'aqrn'

    private const int BufferCount = 3;
    private const int PollIntervalMs = 100;

    [StructLayout(LayoutKind.Sequential)]
    private struct AudioStreamBasicDescription
    {
        public double SampleRate;
        public uint FormatId;
        public uint FormatFlags;
        public uint BytesPerPacket;
        public uint FramesPerPacket;
        public uint BytesPerFrame;
        public uint ChannelsPerFrame;
        public uint BitsPerChannel;
        public uint Reserved;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct AudioQueueBuffer
    {
        public uint AudioDataBytesCapacity;
        public IntPtr AudioData;
        public uint AudioDataByteSize;
        public IntPtr UserData;
        public uint PacketDescriptionCapacity;
        public IntPtr PacketDescriptions;
        public uint PacketDescriptionCount;
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void AudioQueueOutputCallback(IntPtr userData, IntPtr queue, IntPtr buffer);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void AudioQueuePropertyListener(IntPtr userData, IntPtr queue, uint propertyId);

    [DllImport(AudioToolbox, CallingConvention = CallingConvention.Cdecl)]
    private static extern int AudioQueueNewOutput(
        ref AudioStreamBasicDescription format, AudioQueueOutputCallback callback, IntPtr userData,
        IntPtr callbackRunLoop, IntPtr callbackRunLoopMode, uint flags, out IntPtr queue);

    [DllImport(AudioToolbox, CallingConvention = CallingConvention.Cdecl)]
    private static extern int AudioQueueAllocateBuffer(IntPtr queue, uint bufferByteSize, out IntPtr buffer);

    [DllImport(AudioToolbox, CallingConvention = CallingConvention.Cdecl)]
    private static extern int AudioQueueEnqueueBuffer(IntPtr queue, IntPtr buffer, uint packetDescriptionCount, IntPtr packetDescriptions);

    [DllImport(AudioToolbox, CallingConvention = CallingConvention.Cdecl)]
    private static extern int AudioQueueAddPropertyListener(IntPtr queue, uint propertyId, AudioQueuePropertyListener listener, IntPtr userData);

    [DllImport(AudioToolbox, CallingConvention = CallingConvention.Cdecl)]
    private static extern int AudioQueueGetProperty(IntPtr queue, uint propertyId, out uint data, ref uint dataSize);

    [DllImport(AudioToolbox, CallingConvention = CallingConvention.Cdecl)]
    private static extern int AudioQueueStart(IntPtr queue, IntPtr startTime);

    [DllImport(AudioToolbox, CallingConvention = CallingConvention.Cdecl)]
    private static extern int AudioQueueStop(IntPtr queue, [MarshalAs(UnmanagedType.U1)] bool immediate);

    [DllImport(AudioToolbox, CallingConvention = CallingConvention.Cdecl)]
    private static extern int AudioQueueDispose(IntPtr queue, [MarshalAs(UnmanagedType.U1)] bool immediate);

    public Task PlayAudioAsync(short[] audioData, int sampleRate, CancellationToken cancellationToken)
    {
        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (audioData == null || audioData.Length == 0)
            {
                return;
            }

            Play(audioData, sampleRate, cancellationToken);
        }, cancellationToken);
    }

    internal static int BufferSampleCount(int sampleRate) => Math.Max(sampleRate / 5, 1);

    internal static string? DescribeStatus(int status)
    {
        Span<byte> code = stackalloc byte[4];
        System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(code, status);
        foreach (var b in code)
        {
            if (b is < 0x20 or > 0x7E)
            {
                return null;
            }
        }

        return $"'{System.Text.Encoding.ASCII.GetString(code)}'";
    }

    private static void Play(short[] audioData, int sampleRate, CancellationToken cancellationToken)
    {
        var format = new AudioStreamBasicDescription
        {
            SampleRate = sampleRate,
            FormatId = FormatLinearPcm,
            FormatFlags = FormatFlagSignedIntegerPacked,
            BytesPerPacket = 2,
            FramesPerPacket = 1,
            BytesPerFrame = 2,
            ChannelsPerFrame = 1,
            BitsPerChannel = 16,
        };

        var returnedBuffers = new ConcurrentQueue<IntPtr>();
        using var bufferReturned = new SemaphoreSlim(0);
        using var queueStopped = new ManualResetEventSlim(false);
        var queue = IntPtr.Zero;
        var disposeStatus = 0;

        AudioQueueOutputCallback outputCallback = (_, _, buffer) =>
        {
            returnedBuffers.Enqueue(buffer);
            bufferReturned.Release();
        };

        AudioQueuePropertyListener runningListener = (_, listenerQueue, propertyId) =>
        {
            if (propertyId != PropertyIsRunning)
            {
                return;
            }

            uint size = sizeof(uint);
            if (AudioQueueGetProperty(listenerQueue, PropertyIsRunning, out var running, ref size) != 0 || running == 0)
            {
                queueStopped.Set();
            }
        };

        try
        {
            Check(AudioQueueNewOutput(ref format, outputCallback, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 0, out queue),
                "creating the output queue");
            Check(AudioQueueAddPropertyListener(queue, PropertyIsRunning, runningListener, IntPtr.Zero),
                "registering the playback listener");

            var chunkSamples = BufferSampleCount(sampleRate);
            var bufferCount = Math.Min(BufferCount, (audioData.Length + chunkSamples - 1) / chunkSamples);
            var offset = 0;
            var outstanding = 0;

            for (var i = 0; i < bufferCount; i++)
            {
                Check(AudioQueueAllocateBuffer(queue, (uint)(chunkSamples * 2), out var buffer), "allocating an audio buffer");
                FillAndEnqueue(queue, buffer, audioData, ref offset);
                outstanding++;
            }

            Check(AudioQueueStart(queue, IntPtr.Zero), "starting playback");

            while (outstanding > 0)
            {
                if (!bufferReturned.Wait(PollIntervalMs, cancellationToken))
                {
                    if (queueStopped.IsSet)
                    {
                        throw new AudioPlaybackException(Backend, "playing audio", 0, "the output queue stopped unexpectedly");
                    }

                    continue;
                }

                returnedBuffers.TryDequeue(out var returned);
                outstanding--;

                if (offset < audioData.Length)
                {
                    FillAndEnqueue(queue, returned, audioData, ref offset);
                    outstanding++;
                }
            }

            Check(AudioQueueStop(queue, false), "stopping playback");
            queueStopped.Wait(cancellationToken);
        }
        finally
        {
            if (queue != IntPtr.Zero)
            {
                disposeStatus = AudioQueueDispose(queue, true);
            }

            GC.KeepAlive(outputCallback);
            GC.KeepAlive(runningListener);
        }

        Check(disposeStatus, "releasing the output queue");
    }

    private static void FillAndEnqueue(IntPtr queue, IntPtr bufferPointer, short[] audioData, ref int offset)
    {
        var buffer = Marshal.PtrToStructure<AudioQueueBuffer>(bufferPointer);
        var count = Math.Min((int)(buffer.AudioDataBytesCapacity / 2), audioData.Length - offset);

        Marshal.Copy(audioData, offset, buffer.AudioData, count);
        buffer.AudioDataByteSize = (uint)(count * 2);
        Marshal.StructureToPtr(buffer, bufferPointer, false);
        offset += count;

        Check(AudioQueueEnqueueBuffer(queue, bufferPointer, 0, IntPtr.Zero), "queueing audio");
    }

    private static void Check(int status, string operation)
    {
        if (status != 0)
        {
            throw new AudioPlaybackException(Backend, operation, status, DescribeStatus(status));
        }
    }
}
