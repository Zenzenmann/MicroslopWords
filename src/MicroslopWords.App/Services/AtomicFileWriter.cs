namespace MicroslopWords.App.Services;

public sealed class AtomicFileWriter : IAtomicFileWriter
{
    private readonly Action<string, string, bool> _replace;

    public AtomicFileWriter()
        : this(static (source, target, overwrite) => File.Move(source, target, overwrite))
    {
    }

    internal AtomicFileWriter(Action<string, string, bool> replace)
    {
        ArgumentNullException.ThrowIfNull(replace);
        _replace = replace;
    }

    public void Write(
        string targetPath,
        Action<Stream> write,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetPath);
        ArgumentNullException.ThrowIfNull(write);
        cancellationToken.ThrowIfCancellationRequested();

        var fullPath = Path.GetFullPath(targetPath);
        var directory = Path.GetDirectoryName(fullPath);

        if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
        {
            throw new DirectoryNotFoundException("The destination folder does not exist.");
        }

        var temporaryPath = Path.Combine(
            directory,
            $".{Path.GetFileName(fullPath)}.{Guid.NewGuid():N}.tmp");

        var temporaryFileExists = false;

        try
        {
            using (var stream = new FileStream(
                       temporaryPath,
                       FileMode.CreateNew,
                       FileAccess.ReadWrite,
                       FileShare.None,
                       bufferSize: 81920,
                       FileOptions.SequentialScan))
            {
                temporaryFileExists = true;
                write(stream);
                cancellationToken.ThrowIfCancellationRequested();
                stream.Flush(flushToDisk: true);
            }

            cancellationToken.ThrowIfCancellationRequested();
            _replace(temporaryPath, fullPath, true);
            temporaryFileExists = false;
        }
        finally
        {
            if (temporaryFileExists)
            {
                TryDelete(temporaryPath);
            }
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
