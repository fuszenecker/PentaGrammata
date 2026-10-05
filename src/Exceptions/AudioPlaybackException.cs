namespace PentaGrammata.Exceptions;

/// <summary>Reports an error returned by a platform audio backend.</summary>
public sealed class AudioPlaybackException : System.Exception
{
    public string Backend { get; }

    public int NativeErrorCode { get; }

    public AudioPlaybackException(string backend, string operation, int nativeErrorCode, string? nativeMessage = null)
        : base($"{backend} audio {operation} failed with code {nativeErrorCode}"
            + (string.IsNullOrWhiteSpace(nativeMessage) ? "." : $": {nativeMessage}"))
    {
        Backend = backend;
        NativeErrorCode = nativeErrorCode;
    }
}
