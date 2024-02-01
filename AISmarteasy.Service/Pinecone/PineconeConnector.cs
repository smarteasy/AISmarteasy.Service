using System.Collections.Concurrent;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text.Json;
using AISmarteasy.Core;
using Microsoft.Extensions.Logging;

namespace AISmarteasy.Service.Pinecone;

public sealed class PineconeConnector(string environment, string apiKey, HttpClient? httpClient = null) : IVectorDatabaseConnector
{
    private static readonly ILogger Logger = LoggerProvider.Provide();
    private readonly HttpClient _httpClient = HttpClientProvider.GetHttpClient(httpClient);

    private readonly KeyValuePair<string, string> _authHeader = new("Api-Key", apiKey);
    private readonly JsonSerializerOptions _jsonSerializerOptions = MemoryUtil.DefaultSerializerOptions;
    private readonly ConcurrentDictionary<string, string> _indexHostMapping = new();
    private const int MAX_BATCH_SIZE = 100;

    public async Task<int> UpsertAsync(string indexName, IEnumerable<MemoryDocument> vectors,
        string indexNamespace = "", CancellationToken cancellationToken = default)
    {
        Logger.LogDebug("Upserting vectors");

        int totalUpserted = 0;
        int totalBatches = 0;

        string basePath = await GetVectorOperationsApiBasePathAsync(indexName).ConfigureAwait(false);
        IAsyncEnumerable<MemoryDocument> validVectors = MemoryUtil.EnsureValidMetadataAsync(vectors.ToAsyncEnumerable());

        await foreach (UpsertRequest? batch in MemoryUtil.GetUpsertBatchesAsync(validVectors, MAX_BATCH_SIZE).WithCancellation(cancellationToken))
        {
            totalBatches++;

            using HttpRequestMessage request = batch.ToNamespace(indexNamespace).Build();

            string? responseContent;

            try
            {
                (_, responseContent) = await ExecuteHttpRequestAsync(basePath, request, cancellationToken).ConfigureAwait(false);
            }
            catch (HttpOperationException e)
            {
                Logger.LogError(e, "Failed to upsert vectors {Message}", e.Message);
                throw;
            }

            UpsertResponse? data = JsonSerializer.Deserialize<UpsertResponse>(responseContent, _jsonSerializerOptions);

            if (data == null)
            {
                Logger.LogWarning("Unable to deserialize Upsert response");
                continue;
            }

            totalUpserted += data.UpsertedCount;

            Logger.LogDebug("Upserted batch {0} with {1} vectors", totalBatches, data.UpsertedCount);
        }

        Logger.LogDebug("Upserted {0} vectors in {1} batches", totalUpserted, totalBatches);

        return totalUpserted;
    }

    public async IAsyncEnumerable<MemoryDocument?> FetchVectorsAsync(string indexName, IEnumerable<string> ids, string indexNamespace = "",
        bool includeValues = false, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        Logger.LogDebug("Searching vectors by id");

        string basePath = await GetVectorOperationsApiBasePathAsync(indexName).ConfigureAwait(false);

        FetchRequest fetchRequest = FetchRequest.FetchVectors(ids).FromNamespace(indexNamespace);
        using HttpRequestMessage request = fetchRequest.Build();
        
        string? responseContent;

        try
        {
            (_, responseContent) = await ExecuteHttpRequestAsync(basePath, request, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpOperationException e)
        {
            Logger.LogError(e, "Error occurred on Get Vectors request: {Message}", e.Message);
            yield break;
        }

        FetchResponse? data = JsonSerializer.Deserialize<FetchResponse>(responseContent, _jsonSerializerOptions);

        if (data == null)
        {
            Logger.LogWarning("Unable to deserialize Get response");
            yield break;
        }

        if (data.Vectors.Count == 0)
        {
            Logger.LogWarning("Vectors not found");
            yield break;
        }

        IEnumerable<MemoryDocument> records = includeValues ? data.Vectors.Values : data.WithoutEmbeddings();

        foreach (var record in records)
        {
            yield return record;
        }
    }
    
    public async IAsyncEnumerable<MemoryDocument?> QueryAsync(string indexName, int topK, MemoryQuery query,
         bool includeMetadata = true, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        Logger.LogDebug("Querying top {0} nearest vectors", topK);

        using HttpRequestMessage request = query.Build();
        string basePath = await GetVectorOperationsApiBasePathAsync(indexName).ConfigureAwait(false);

        string? responseContent;

        try
        {
            (_, responseContent) = await ExecuteHttpRequestAsync(basePath, request, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpOperationException e)
        {
            Logger.LogError(e, "Error occurred on Query Vectors request: {Message}", e.Message);
            yield break;
        }

        MemoryQueryResponse? queryResponse = JsonSerializer.Deserialize<MemoryQueryResponse>(responseContent, _jsonSerializerOptions);

        if (queryResponse == null)
        {
            Logger.LogWarning("Unable to deserialize Query response");
            yield break;
        }

        if (queryResponse.Matches.Count == 0)
        {
            Logger.LogWarning("No matches found");
            yield break;
        }

        foreach (MemoryDocument? match in queryResponse.Matches)
        {
            yield return match;
        }
    }

    public async IAsyncEnumerable<(MemoryDocument, double)> GetMostRelevantAsync(string indexName, string indexNamespace, ReadOnlyMemory<float>? vector,
        int topK, double threshold, bool includeMetadata, Dictionary<string, object>? filter = default, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        Logger.LogDebug("Searching top {0} nearest vectors with threshold {1}", topK, threshold);

        List<(MemoryDocument document, float score)> documents = new();

        MemoryQuery query = MemoryQuery.Create(topK)
            .InNamespace(indexNamespace)
            .WithFilter(filter);

        if (vector != null)
        {
            query.WithVector(vector);
        }

        IAsyncEnumerable<MemoryDocument?> matches = QueryAsync(indexName, topK, query, includeMetadata, cancellationToken);

        await foreach (MemoryDocument? match in matches)
        {
            if (match == null)
            {
                continue;
            }

            if (match.Score > threshold)
            {
                documents.Add((match, match.Score ?? 0));
            }
        }

        if (documents.Count == 0)
        {
            Logger.LogWarning("No relevant documents found");
            yield break;
        }

        documents = documents.OrderByDescending(x => x.score).ToList();

        foreach ((MemoryDocument document, float score) in documents)
        {
            yield return (document, score);
        }
    }

    public async Task DeleteAsync(string indexName,
        IEnumerable<string>? ids = null, string indexNamespace = "", Dictionary<string, object>? filter = null,
        bool deleteAll = false, CancellationToken cancellationToken = default)
    {
        if (ids == null && string.IsNullOrEmpty(indexNamespace) && filter == null && !deleteAll)
        {
            throw new ArgumentException("Must provide at least one of ids, filter, or deleteAll");
        }

        ids = ids?.ToList();

        DeleteRequest deleteRequest = deleteAll ? string.IsNullOrEmpty(indexNamespace)
                ? DeleteRequest.GetDeleteAllVectorsRequest()
                : DeleteRequest.ClearNamespace(indexNamespace)
            : DeleteRequest.DeleteVectors(ids).FromNamespace(indexNamespace).FilterBy(filter);

        Logger.LogDebug("Delete operation for Index {0}: {1}", indexName, deleteRequest.ToString());

        string basePath = await GetVectorOperationsApiBasePathAsync(indexName).ConfigureAwait(false);

        using HttpRequestMessage request = deleteRequest.Build();

        try
        {
            await ExecuteHttpRequestAsync(basePath, request, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpOperationException e)
        {
            Logger.LogError(e, "Delete operation failed: {Message}", e.Message);
            throw;
        }
    }

    public async Task UpdateAsync(string indexName, MemoryDocument document, string indexNamespace = "", CancellationToken cancellationToken = default)
    {
        Logger.LogDebug("Updating vector: {0}", document.Id);

        string basePath = await GetVectorOperationsApiBasePathAsync(indexName).ConfigureAwait(false);

        using HttpRequestMessage request = UpdateVectorRequest
            .FromMemoryDocument(document)
            .InNamespace(indexNamespace)
            .Build();

        try
        {
            await ExecuteHttpRequestAsync(basePath, request, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpOperationException e)
        {
            Logger.LogError(e, "Vector update for Document {Id} failed. {Message}", document.Id, e.Message);
            throw;
        }
    }

    public async Task<IndexStats?> DescribeIndexStatsAsync(string indexName, 
        Dictionary<string, object>? filter = default, CancellationToken cancellationToken = default)
    {
        Logger.LogDebug("Getting index stats for index {0}", indexName);

        string basePath = await GetVectorOperationsApiBasePathAsync(indexName).ConfigureAwait(false);

        using HttpRequestMessage request = DescribeIndexStatsRequest.GetIndexStats()
            .WithFilter(filter)
            .Build();

        string? responseContent;

        try
        {
            (_, responseContent) = await ExecuteHttpRequestAsync(basePath, request, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpOperationException e)
        {
            Logger.LogError(e, "Index not found {Message}", e.Message);
            throw;
        }

        IndexStats? result = JsonSerializer.Deserialize<IndexStats>(responseContent, _jsonSerializerOptions);

        if (result != null)
        {
           Logger.LogDebug("Index stats retrieved");
        }
        else
        {
           Logger.LogWarning("Index stats retrieval failed");
        }

        return result;
    }

    public async IAsyncEnumerable<string?> ListIndexesAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using HttpRequestMessage request = ListIndexesRequest.Create().Build();

        (HttpResponseMessage _, string responseContent) = await ExecuteHttpRequestAsync(GetIndexOperationsApiBasePath(), request, cancellationToken).ConfigureAwait(false);

        string[]? indices = JsonSerializer.Deserialize<string[]?>(responseContent, _jsonSerializerOptions);

        if (indices == null)
        {
            yield break;
        }

        foreach (string? index in indices)
        {
            yield return index;
        }
    }

    public async Task CreateIndexAsync(IndexDefinition indexDefinition, CancellationToken cancellationToken = default)
    {
        Logger.LogDebug("Creating index {0}", indexDefinition.ToString());

        using HttpRequestMessage request = indexDefinition.Build();

        try
        {
            await ExecuteHttpRequestAsync(GetIndexOperationsApiBasePath(), request, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpOperationException e) when (e.StatusCode == HttpStatusCode.BadRequest)
        {
            Logger.LogError(e, "Bad Request: {StatusCode}, {Response}", e.StatusCode, e.ResponseContent);
            throw;
        }
        catch (HttpOperationException e) when (e.StatusCode == HttpStatusCode.Conflict)
        {
            Logger.LogError(e, "Index of given name already exists: {StatusCode}, {Response}", e.StatusCode, e.ResponseContent);
            throw;
        }
        catch (HttpOperationException e)
        {
            Logger.LogError(e, "Creating index failed: {Message}, {Response}", e.Message, e.ResponseContent);
            throw;
        }
    }

    public async Task DeleteIndexAsync(string indexName, CancellationToken cancellationToken = default)
    {
        Logger.LogDebug("Deleting index {0}", indexName);

        using HttpRequestMessage request = DeleteIndexRequest.Create(indexName).Build();

        try
        {
            await ExecuteHttpRequestAsync(GetIndexOperationsApiBasePath(), request, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpOperationException e) when (e.StatusCode == HttpStatusCode.NotFound)
        {
            Logger.LogError(e, "Index Not Found: {StatusCode}, {Response}", e.StatusCode, e.ResponseContent);
            throw;
        }
        catch (HttpOperationException e)
        {
            Logger.LogError(e, "Deleting index failed: {Message}, {Response}", e.Message, e.ResponseContent);
            throw;
        }

        Logger.LogDebug("Index: {0} has been successfully deleted.", indexName);
    }

    public async Task<bool> DoesIndexExistAsync(string indexName, CancellationToken cancellationToken = default)
    {
        Logger.LogDebug("Checking for index {0}", indexName);

        List<string?> indexNames = await ListIndexesAsync(cancellationToken).ToListAsync(cancellationToken).ConfigureAwait(false);

        if (indexNames.All(name => name != indexName))
        {
            return false;
        }

        MemoryIndex? index = await DescribeIndexAsync(indexName, cancellationToken).ConfigureAwait(false);

        return index is { Status.State: IndexState.Ready };
    }

    public async Task<MemoryIndex?> DescribeIndexAsync(string indexName, CancellationToken cancellationToken = default)
    {
        Logger.LogDebug("Getting Description for Index: {0}", indexName);

        using HttpRequestMessage request = DescribeIndexRequest.Create(indexName).Build();

        string? responseContent;

        try
        {
            (_, responseContent) = await ExecuteHttpRequestAsync(GetIndexOperationsApiBasePath(), request, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpOperationException e) when (e.StatusCode == HttpStatusCode.BadRequest)
        {
            Logger.LogError(e, "Bad Request: {StatusCode}, {Response}", e.StatusCode, e.ResponseContent);
            throw;
        }
        catch (HttpOperationException e)
        {
            Logger.LogError(e, "Describe index failed: {Message}, {Response}", e.Message, e.ResponseContent);
            throw;
        }

        MemoryIndex? indexDescription = JsonSerializer.Deserialize<MemoryIndex>(responseContent, _jsonSerializerOptions);

        if (indexDescription == null)
        {
            Logger.LogDebug("Deserialized index description is null");
        }

        return indexDescription;
    }

    public async Task ConfigureIndexAsync(string indexName, int replicas = 1, PodTypeKind podType = PodTypeKind.P1X1, CancellationToken cancellationToken = default)
    {
        Logger.LogDebug("Configuring index {0}", indexName);

        using HttpRequestMessage request = ConfigureIndexRequest
            .Create(indexName)
            .WithPodType(podType)
            .NumberOfReplicas(replicas)
            .Build();

        try
        {
            await ExecuteHttpRequestAsync(GetIndexOperationsApiBasePath(), request, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpOperationException e) when (e.StatusCode == HttpStatusCode.BadRequest)
        {
            Logger.LogError(e, "Request exceeds quota or collection name is invalid. {Index}", indexName);
            throw;
        }
        catch (HttpOperationException e) when (e.StatusCode == HttpStatusCode.NotFound)
        {
            Logger.LogError(e, "Index not found. {Index}", indexName);
            throw;
        }
        catch (HttpOperationException e)
        {
            Logger.LogError(e, "Index configuration failed: {Message}, {Response}", e.Message, e.ResponseContent);
            throw;
        }

        Logger.LogDebug("Collection created. {0}", indexName);
    }

    private async Task<string> GetVectorOperationsApiBasePathAsync(string indexName)
    {
        string indexHost = await GetIndexHostAsync(indexName).ConfigureAwait(false);
        return $"https://{indexHost}";
    }

    private string GetIndexOperationsApiBasePath()
    {
        return $"https://controller.{environment}.pinecone.io";
    }

    private async Task<(HttpResponseMessage response, string responseContent)> ExecuteHttpRequestAsync(
        string baseUrl, HttpRequestMessage request, CancellationToken cancellationToken = default)
    {
        request.Headers.Add(_authHeader.Key, _authHeader.Value);
        request.RequestUri = new Uri(baseUrl + request.RequestUri);

        using HttpResponseMessage response = await _httpClient.SendWithSuccessCheckAsync(request, cancellationToken).ConfigureAwait(false);

        string responseContent = await response.Content.ReadAsStringWithExceptionMappingAsync().ConfigureAwait(false);

        return (response, responseContent);
    }

    private async Task<string> GetIndexHostAsync(string indexName, CancellationToken cancellationToken = default)
    {
        if (_indexHostMapping.TryGetValue(indexName, out string? indexHost))
        {
            return indexHost;
        }

        Logger.LogDebug("Getting index host from Pinecone.");

        MemoryIndex? pineconeIndex = await DescribeIndexAsync(indexName, cancellationToken).ConfigureAwait(false);

        if (pineconeIndex == null)
        {
            throw new CoreException("Index not found in Pinecone. Create index to perform operations with vectors.");
        }

        if (string.IsNullOrWhiteSpace(pineconeIndex.Status.Host))
        {
            throw new CoreException($"Host of index {indexName} is unknown.");
        }

        Logger.LogDebug("Found host {0} for index {1}", pineconeIndex.Status.Host, indexName);

        _indexHostMapping.TryAdd(indexName, pineconeIndex.Status.Host);

        return pineconeIndex.Status.Host;
    }
}
