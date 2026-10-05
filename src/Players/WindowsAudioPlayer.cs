using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.ExceptionServices;

using PentaGrammata.Exceptions;
using PentaGrammata.Interfaces;

namespace PentaGrammata.Players;

public class WindowsAudioPlayer : IAudioPlayer
{
    private const int WAVE_MAPPER = -1;
    private const int WAVE_FORMAT_PCM = 1;
    private const int CALLBACK_EVENT = 0x00050000;
    private const int MMSYSERR_NOERROR = 0;

    [StructLayout(LayoutKind.Sequential)]
    private struct WaveFormatEx
    {
        public ushort wFormatTag;
        public ushort nChannels;
        public uint nSamplesPerSec;
        public uint nAvgBytesPerSec;
        public ushort nBlockAlign;
        public ushort wBitsPerSample;
        public ushort cbSize;
    }

    [DllImport("winmm.dll")]
    private static extern int waveOutOpen(out IntPtr hWaveOut, int uDeviceID, ref WaveFormatEx lpFormat, IntPtr dwCallback, IntPtr dwInstance, int dwFlags);

    [DllImport("winmm.dll")]
    private static extern int waveOutPrepareHeader(IntPtr hWaveOut, IntPtr lpWaveOutHdr, int uSize);

    [DllImport("winmm.dll")]
    private static extern int waveOutUnprepareHeader(IntPtr hWaveOut, IntPtr lpWaveOutHdr, int uSize);

    [DllImport("winmm.dll")]
    private static extern int waveOutWrite(IntPtr hWaveOut, IntPtr lpWaveOutHdr, int uSize);

    [DllImport("winmm.dll")]
    private static extern int waveOutReset(IntPtr hWaveOut);

    [DllImport("winmm.dll")]
    private static extern int waveOutClose(IntPtr hWaveOut);

    [StructLayout(LayoutKind.Sequential)]
    private struct WaveHdr
    {
        public IntPtr lpData;
        public uint dwBufferLength;
        public uint dwBytesRecorded;
        public IntPtr dwUser;
        public uint dwFlags;
        public uint dwLoops;
        public IntPtr lpNext;
        public IntPtr reserved;
    }

    public Task PlayAudioAsync(short[] audioData, int sampleRate, CancellationToken cancellationToken)
    {
        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (audioData == null || audioData.Length == 0)
            {
                return;
            }

            var format = new WaveFormatEx
            {
                wFormatTag = WAVE_FORMAT_PCM,
                nChannels = 1,
                nSamplesPerSec = (uint)sampleRate,
                nAvgBytesPerSec = (uint)(sampleRate * 2),
                nBlockAlign = 2,
                wBitsPerSample = 16,
                cbSize = 0,
            };

            using var doneEvent = new ManualResetEvent(false);
            var callbackHandle = doneEvent.SafeWaitHandle.DangerousGetHandle();

            var bytes = new byte[audioData.Length * 2];
            Buffer.BlockCopy(audioData, 0, bytes, 0, bytes.Length);

            IntPtr hWaveOut = IntPtr.Zero;
            IntPtr dataPtr = IntPtr.Zero;
            IntPtr headerPtr = IntPtr.Zero;
            bool opened = false;
            bool prepared = false;
            Exception? playbackException = null;

            try
            {
                CheckResult(waveOutOpen(out hWaveOut, WAVE_MAPPER, ref format, callbackHandle, IntPtr.Zero, CALLBACK_EVENT), "opening the device");
                opened = true;

                dataPtr = Marshal.AllocHGlobal(bytes.Length);
                Marshal.Copy(bytes, 0, dataPtr, bytes.Length);

                var header = new WaveHdr
                {
                    lpData = dataPtr,
                    dwBufferLength = (uint)bytes.Length,
                };

                headerPtr = Marshal.AllocHGlobal(Marshal.SizeOf<WaveHdr>());
                Marshal.StructureToPtr(header, headerPtr, false);

                int headerSize = Marshal.SizeOf<WaveHdr>();
                CheckResult(waveOutPrepareHeader(hWaveOut, headerPtr, headerSize), "preparing the audio buffer");
                prepared = true;

                doneEvent.Reset();
                CheckResult(waveOutWrite(hWaveOut, headerPtr, headerSize), "writing audio");

                while (!doneEvent.WaitOne(10))
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        CheckResult(waveOutReset(hWaveOut), "stopping cancelled audio");
                        cancellationToken.ThrowIfCancellationRequested();
                    }
                }

                cancellationToken.ThrowIfCancellationRequested();
            }
            catch (Exception ex)
            {
                playbackException = ex;
            }
            finally
            {
                if (opened && playbackException is not null)
                {
                    RecordCleanupResult(waveOutReset(hWaveOut), "resetting the device", ref playbackException);
                }

                if (prepared)
                {
                    RecordCleanupResult(waveOutUnprepareHeader(hWaveOut, headerPtr, Marshal.SizeOf<WaveHdr>()), "unpreparing the audio buffer", ref playbackException);
                }

                if (opened)
                {
                    RecordCleanupResult(waveOutClose(hWaveOut), "closing the device", ref playbackException);
                }

                if (headerPtr != IntPtr.Zero)
                    Marshal.FreeHGlobal(headerPtr);
                if (dataPtr != IntPtr.Zero)
                    Marshal.FreeHGlobal(dataPtr);
            }

            if (playbackException is not null)
                ExceptionDispatchInfo.Capture(playbackException).Throw();
        }, cancellationToken);
    }

    private static void CheckResult(int result, string operation)
    {
        if (result != MMSYSERR_NOERROR)
        {
            throw CreateAudioException(operation, result);
        }
    }

    private static void RecordCleanupResult(int result, string operation, ref Exception? playbackException)
    {
        if (result != MMSYSERR_NOERROR && playbackException is null)
        {
            playbackException = CreateAudioException(operation, result);
        }
    }

    private static AudioPlaybackException CreateAudioException(string operation, int error) =>
        new("Windows waveOut", operation, error);
}
