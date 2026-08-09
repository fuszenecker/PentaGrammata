using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

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

            try
            {
                if (waveOutOpen(out hWaveOut, WAVE_MAPPER, ref format, callbackHandle, IntPtr.Zero, CALLBACK_EVENT) != MMSYSERR_NOERROR)
                {
                    System.Diagnostics.Debug.WriteLine("Error opening waveOut device.");
                    return;
                }

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
                waveOutPrepareHeader(hWaveOut, headerPtr, headerSize);

                doneEvent.Reset();
                waveOutWrite(hWaveOut, headerPtr, headerSize);

                while (!doneEvent.WaitOne(10))
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        waveOutReset(hWaveOut);
                        break;
                    }
                }

                waveOutUnprepareHeader(hWaveOut, headerPtr, headerSize);
                cancellationToken.ThrowIfCancellationRequested();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error playing audio: {ex.Message}");
            }
            finally
            {
                if (headerPtr != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(headerPtr);
                }

                if (dataPtr != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(dataPtr);
                }

                if (hWaveOut != IntPtr.Zero)
                {
                    waveOutClose(hWaveOut);
                }
            }
        }, cancellationToken);
    }
}
