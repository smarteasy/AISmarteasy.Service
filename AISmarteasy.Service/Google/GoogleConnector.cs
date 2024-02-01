using AISmarteasy.Core;
using Google.Apis.CustomSearchAPI.v1;
using Google.Apis.Services;

namespace AISmarteasy.Service.Google;

public sealed class GoogleConnector : IWebSearchEngineConnector
{
    private readonly CustomSearchAPIService _search;
    private readonly string? _searchEngineId;

    public GoogleConnector(string apiKey, string searchEngineId) :
        this(new BaseClientService.Initializer { ApiKey = apiKey }, searchEngineId)
    {
        Verifier.NotNullOrWhitespace(apiKey);
    }

    public GoogleConnector(BaseClientService.Initializer initializer, string searchEngineId)
    {
        Verifier.NotNull(initializer);
        Verifier.NotNullOrWhitespace(searchEngineId);

        _search = new CustomSearchAPIService(initializer);
        _searchEngineId = searchEngineId;
    }

    public async Task<IEnumerable<string>> SearchAsync(string query, int count, int offset, CancellationToken cancellationToken)
    {
        if (count is <= 0 or > 10)
        {
            throw new ArgumentOutOfRangeException(nameof(count), count,
                $"{nameof(count)} value must be must be greater than 0 and less than or equals 10.");
        }

        if (offset < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(offset));
        }

        var search = _search.Cse.List();
        search.Cx = _searchEngineId;
        search.Q = query;
        search.Num = count;
        search.Start = offset;

        var results = await search.ExecuteAsync(cancellationToken).ConfigureAwait(false);

        return results.Items.Select(item => item.Snippet);
    }
}