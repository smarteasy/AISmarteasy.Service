using AISmarteasy.Core;

namespace AISmarteasy.Service;

public class ImageConnector
{
    public async Task<string> ImageToTextAsync(IOcrEngine engine, string filename, CancellationToken cancellationToken = default)
    {
        var content = File.OpenRead(filename);
        await using (content.ConfigureAwait(false))
        {
            return await ImageToTextAsync(engine, content, cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task<string> ImageToTextAsync(IOcrEngine engine, BinaryData data, CancellationToken cancellationToken = default)
    {
        var content = data.ToStream();
        await using (content.ConfigureAwait(false))
        {
            return await ImageToTextAsync(engine, content, cancellationToken).ConfigureAwait(false);
        }
    }

    public Task<string> ImageToTextAsync(IOcrEngine engine, Stream data, CancellationToken cancellationToken = default)
    {
        return engine.ExtractTextFromImageAsync(data, cancellationToken);
    }
}
