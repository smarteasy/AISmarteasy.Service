using AISmarteasy.Core;

namespace AISmarteasy.Service;

public class LocalFileSystemConnector : IFileSystemConnector
{
    public Task<Stream> GetFileContentStreamAsync(string filePath, CancellationToken cancellationToken = default)
    {
        try
        {
            return Task.FromResult<Stream>(File.Open(Environment.ExpandEnvironmentVariables(filePath), FileMode.Open, FileAccess.Read));
        }
        catch (Exception e)
        {
            return Task.FromException<Stream>(e);
        }
    }

    public Task<Stream> GetWriteableFileStreamAsync(string filePath, CancellationToken cancellationToken = default)
    {
        try
        {
            return Task.FromResult<Stream>(File.Open(Environment.ExpandEnvironmentVariables(filePath), FileMode.Open, FileAccess.ReadWrite));
        }
        catch (Exception e)
        {
            return Task.FromException<Stream>(e);
        }
    }

    public Task<Stream> CreateFileAsync(string filePath, CancellationToken cancellationToken = default)
    {
        try
        {
            return Task.FromResult<Stream>(File.Create(Environment.ExpandEnvironmentVariables(filePath)));
        }
        catch (Exception e)
        {
            return Task.FromException<Stream>(e);
        }
    }

    public Task<bool> FileExistsAsync(string filePath, CancellationToken cancellationToken = default)
    {
        try
        {
            return Task.FromResult(File.Exists(Environment.ExpandEnvironmentVariables(filePath)));
        }
        catch (Exception e)
        {
            return Task.FromException<bool>(e);
        }
    }
}
