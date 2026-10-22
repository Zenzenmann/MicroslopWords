namespace MicroslopWords.App.Services;

public interface IAtomicFileWriter
{
    void Write(string targetPath, Action<Stream> write, CancellationToken cancellationToken = default);
}
