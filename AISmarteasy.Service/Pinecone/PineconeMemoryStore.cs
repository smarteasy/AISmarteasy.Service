using System.Runtime.CompilerServices;
using AISmarteasy.Core;
using Microsoft.Extensions.Logging;

namespace AISmarteasy.Service.Pinecone;

public class PineconeMemoryStore(string environment, string apiKey)
    : IPineconeMemoryStore
{
    private readonly PineconeConnector _pineconeConnector = new(environment, apiKey);
    private readonly ILogger _logger = LoggerProvider.Provide();

    public async Task CreateCollectionAsync(string collectionName, CancellationToken cancellationToken = default)
    {
        if (!await DoesCollectionExistAsync(collectionName, cancellationToken).ConfigureAwait(false))
        {
            throw new CoreException("Index creation is not supported within memory store. " +
                $"It should be created manually or using {nameof(IPineconeConnector.CreateIndexAsync)}. " +
                $"Ensure index state is {IndexState.Ready}.");
        }
    }

    public async IAsyncEnumerable<string> GetCollectionsAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var index in _pineconeConnector.ListIndexesAsync(cancellationToken).ConfigureAwait(false))
        {
            yield return index ?? "";
        }
    }

    public async Task<bool> DoesCollectionExistAsync(string collectionName, CancellationToken cancellationToken = default)
    {
        return await _pineconeConnector.DoesIndexExistAsync(collectionName, cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteCollectionAsync(string collectionName, CancellationToken cancellationToken = default)
    {
        if (await DoesCollectionExistAsync(collectionName, cancellationToken).ConfigureAwait(false))
        {
            await _pineconeConnector.DeleteIndexAsync(collectionName, cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task<string> UpsertAsync(string collectionName, MemoryRecord record, CancellationToken cancellationToken = default)
    {
        return await UpsertToNamespaceAsync(collectionName, string.Empty, record, cancellationToken).ConfigureAwait(false);
    }

    public async IAsyncEnumerable<string> UpsertBatchAsync(string collectionName, IEnumerable<MemoryRecord> records,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (string id in UpsertBatchToNamespaceAsync(collectionName, string.Empty, records, cancellationToken).ConfigureAwait(false))
        {
            yield return id;
        }
    }

    public async Task<MemoryRecord?> GetAsync(string collectionName, string key, CancellationToken cancellationToken = default)
    {
        return await GetFromNamespaceAsync(collectionName, string.Empty, key, cancellationToken).ConfigureAwait(false);
    }

    public async IAsyncEnumerable<MemoryRecord> GetBatchAsync(string collectionName, IEnumerable<string> keys, 
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (MemoryRecord? record in GetBatchFromNamespaceAsync(collectionName, string.Empty, keys, cancellationToken).ConfigureAwait(false))
        {
            yield return record;
        }
    }

    public async Task<string> UpsertToNamespaceAsync(string indexName, string indexNamespace, MemoryRecord record, CancellationToken cancellationToken = default)
    {
        (MemoryDocument vectorData, OperationTypeKind operationType) = await EvaluateAndUpdateMemoryRecordAsync(indexName, record, indexNamespace, cancellationToken).ConfigureAwait(false);

        Task request = operationType switch
        {
            OperationTypeKind.Upsert => _pineconeConnector.UpsertAsync(indexName, new[] { vectorData }, indexNamespace, cancellationToken),
            OperationTypeKind.Update => _pineconeConnector.UpdateAsync(indexName, vectorData, indexNamespace, cancellationToken),
            OperationTypeKind.Skip => Task.CompletedTask,
            _ => Task.CompletedTask
        };

        try
        {
            await request.ConfigureAwait(false);
        }
        catch (HttpOperationException ex)
        {
            _logger.LogError(ex, "Failed to upsert: {Message}", ex.Message);
            throw;
        }

        return vectorData.Id;
    }

    public async IAsyncEnumerable<MemoryRecord> GetBatchFromNamespaceAsync(string indexName, string indexNamespace, IEnumerable<string> keys,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        foreach (string? key in keys)
        {
            MemoryRecord? record = await GetFromNamespaceAsync(indexName, indexNamespace, key, cancellationToken).ConfigureAwait(false);

            if (record != null)
            {
                yield return record;
            }
        }
    }

    public async IAsyncEnumerable<MemoryRecord?> GetWithDocumentIdAsync(string indexName, string documentId,
        int topK = 3, string indexNamespace = "", [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (MemoryRecord? record in GetWithDocumentIdBatchAsync(indexName, new[] { documentId }, topK, indexNamespace, cancellationToken).ConfigureAwait(false))
        {
            yield return record;
        }
    }

    public async IAsyncEnumerable<MemoryRecord?> GetBatchWithFilterAsync(string indexName, Dictionary<string, object> filter,
        int topK = 10, string indexNamespace = "", [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        IEnumerable<MemoryDocument?> vectorDataList;

        try
        {
            MemoryQuery query = MemoryQuery.Create()
                .InNamespace(indexNamespace)
                .WithFilter(filter);

            vectorDataList = await _pineconeConnector
                .QueryAsync(indexName, topK, query, cancellationToken: cancellationToken)
                .ToListAsync(cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }
        catch (HttpOperationException ex)
        {
            _logger.LogError(ex, "Error getting batch with filter from Pinecone: {Message}", ex.Message);
            throw;
        }

        foreach (MemoryDocument? record in vectorDataList)
        {
            yield return record?.ToMemoryRecord(transferVectorOwnership: true);
        }
    }

    public async IAsyncEnumerable<string> UpsertBatchToNamespaceAsync(string indexName, string indexNamespace, IEnumerable<MemoryRecord> records, 
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        List<MemoryDocument> upsertDocuments = new();
        List<MemoryDocument> updateDocuments = new();

        foreach (MemoryRecord? record in records)
        {
            (MemoryDocument document, OperationTypeKind operationType) = await EvaluateAndUpdateMemoryRecordAsync(
                indexName,
                record,
                indexNamespace,
                cancellationToken).ConfigureAwait(false);

            switch (operationType)
            {
                case OperationTypeKind.Upsert:
                    upsertDocuments.Add(document);
                    break;

                case OperationTypeKind.Update:

                    updateDocuments.Add(document);
                    break;

                case OperationTypeKind.Skip:
                    yield return document.Id;
                    break;
            }
        }

        List<Task> tasks = new();

        if (upsertDocuments.Count > 0)
        {
            tasks.Add(_pineconeConnector.UpsertAsync(indexName, upsertDocuments, indexNamespace, cancellationToken));
        }

        if (updateDocuments.Count > 0)
        {
            IEnumerable<Task> updates = updateDocuments.Select(async d
                => await _pineconeConnector.UpdateAsync(indexName, d, indexNamespace, cancellationToken).ConfigureAwait(false));

            tasks.AddRange(updates);
        }

        MemoryDocument[] vectorData = upsertDocuments.Concat(updateDocuments).ToArray();

        try
        {
            await Task.WhenAll(tasks).ConfigureAwait(false);
        }
        catch (HttpOperationException ex)
        {
            _logger.LogError(ex, "Failed to upsert batch: {Message}", ex.Message);
            throw;
        }

        foreach (MemoryDocument? v in vectorData)
        {
            yield return v.Id;
        }
    }

    public async Task<MemoryRecord?> GetFromNamespaceAsync(string indexName, string indexNamespace, string key, CancellationToken cancellationToken = default)
    {
        try
        {
            bool withEmbedding = true;
            await foreach (MemoryDocument? record in _pineconeConnector.FetchVectorsAsync(indexName, new[] { key },
                               indexNamespace, withEmbedding, cancellationToken))
            {
                return record?.ToMemoryRecord(transferVectorOwnership: true);
            }
        }
        catch (HttpOperationException ex)
        {
            _logger.LogError(ex, "Failed to get vector data from Pinecone: {Message}", ex.Message);
            throw;
        }

        return null;
    }

    public async IAsyncEnumerable<MemoryRecord?> GetWithDocumentIdBatchAsync(string indexName, IEnumerable<string> documentIds, int limit = 3, 
        string indexNamespace = "", [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        foreach (IAsyncEnumerable<MemoryRecord?>? records
                 in documentIds.Select(
                     documentId => GetWithDocumentIdAsync(indexName, documentId, limit, indexNamespace, cancellationToken)))
        {
            await foreach (MemoryRecord? record in records.WithCancellation(cancellationToken))
            {
                yield return record;
            }
        }
    }

    public async Task RemoveAsync(string collectionName, string key, CancellationToken cancellationToken = default)
    {
        await RemoveFromNamespaceAsync(collectionName, string.Empty, key, cancellationToken).ConfigureAwait(false);
    }

    public async Task RemoveFromNamespaceAsync(string indexName, string indexNamespace, string key, CancellationToken cancellationToken = default)
    {
        try
        {
            await _pineconeConnector.DeleteAsync(indexName, new[]
                {
                    key
                },
                indexNamespace,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (HttpOperationException ex)
        {
            _logger.LogError(ex, "Failed to remove vector data from Pinecone: {Message}", ex.Message);
            throw;
        }
    }

    public async Task RemoveBatchAsync(string collectionName, IEnumerable<string> keys, CancellationToken cancellationToken = default)
    {
        await RemoveBatchFromNamespaceAsync(collectionName, string.Empty, keys, cancellationToken).ConfigureAwait(false);
    }


    public async Task RemoveBatchFromNamespaceAsync(string collectionName, string collectionNamespace, IEnumerable<string> keys, CancellationToken cancellationToken = default)
    {
        await Task.WhenAll(keys.Select(async k => await RemoveFromNamespaceAsync(collectionName, collectionNamespace, k, cancellationToken).ConfigureAwait(false))).ConfigureAwait(false);
    }

    public async Task RemoveWithFilterAsync(string indexName, Dictionary<string, object> filter,
        string indexNamespace = "", CancellationToken cancellationToken = default)
    {
        try
        {
            await _pineconeConnector.DeleteAsync(
                indexName,
                default,
                indexNamespace,
                filter,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (HttpOperationException ex)
        {
            _logger.LogError(ex, "Failed to remove vector data from Pinecone: {Message}", ex.Message);
            throw;
        }
    }

    public IAsyncEnumerable<(MemoryRecord, double)> GetNearestMatchesAsync(string collectionName, string collectionNamespace, ReadOnlyMemory<float> vector, int topK,
        double minRelevanceScore = 0, bool isIncludeMetadata = true, CancellationToken cancellationToken = default)
    {
        return GetNearestMatchesFromNamespaceAsync(collectionName, collectionNamespace, vector, topK, minRelevanceScore, isIncludeMetadata, cancellationToken);
    }

    private async IAsyncEnumerable<(MemoryRecord, double)> GetNearestMatchesFromNamespaceAsync(string collectionName, string collectionNamespace, ReadOnlyMemory<float> vector, int topK,
        double minRelevanceScore, bool isIncludeMetadata, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        IAsyncEnumerable<(MemoryDocument, double)> results = _pineconeConnector.GetMostRelevantAsync(collectionName, collectionNamespace, vector,
            topK, minRelevanceScore,isIncludeMetadata, default, cancellationToken);

        await foreach ((MemoryDocument, double) result in results)
        {
            yield return (result.Item1.ToMemoryRecord(transferVectorOwnership: true), result.Item2);
        }
    }




    private async IAsyncEnumerable<(MemoryRecord, double)> GetNearestMatchesWithFilterAsync(string collectionName, string collectionNamespace, ReadOnlyMemory<float> vector,int topK,
 Dictionary<string, object> filter, double minRelevanceScore = 0D, bool includeMetadata = true, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        IAsyncEnumerable<(MemoryDocument, double)> results = _pineconeConnector.GetMostRelevantAsync(collectionName, collectionNamespace, vector,
            topK, minRelevanceScore, includeMetadata, filter, cancellationToken);

        await foreach ((MemoryDocument, double) result in results)
        {
            yield return (result.Item1.ToMemoryRecord(transferVectorOwnership: true), result.Item2);
        }
    }







    public async Task RemoveWithDocumentIdAsync(string indexName, string documentId, string indexNamespace, CancellationToken cancellationToken = default)
    {
        try
        {
            await _pineconeConnector.DeleteAsync(indexName, null, indexNamespace, new Dictionary<string, object>()
            {
                { "document_Id", documentId }
            }, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (HttpOperationException ex)
        {
            _logger.LogError(ex, "Failed to remove vector data from Pinecone: {Message}", ex.Message);
            throw;
        }
    }

    public async Task RemoveWithDocumentIdBatchAsync(string indexName, IEnumerable<string> documentIds, string indexNamespace,
        CancellationToken cancellationToken = default)
    {
        try
        {
            IEnumerable<Task> tasks = documentIds.Select(async id
                => await RemoveWithDocumentIdAsync(indexName, id, indexNamespace, cancellationToken)
                    .ConfigureAwait(false));

            await Task.WhenAll(tasks).ConfigureAwait(false);
        }
        catch (HttpOperationException ex)
        {
            _logger.LogError(ex, "Error in batch removing data from Pinecone: {Message}", ex.Message);
            throw;
        }
    }


    public async Task ClearNamespaceAsync(string indexName, string indexNamespace, CancellationToken cancellationToken = default)
    {
        await _pineconeConnector.DeleteAsync(indexName, default, indexNamespace, null, true, cancellationToken).ConfigureAwait(false);
    }

    public async IAsyncEnumerable<string?> ListNamespacesAsync(string indexName, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        IndexStats? indexStats = await _pineconeConnector.DescribeIndexStatsAsync(indexName, default, cancellationToken).ConfigureAwait(false);

        if (indexStats is null)
        {
            yield break;
        }

        foreach (string? indexNamespace in indexStats.Namespaces.Keys)
        {
            yield return indexNamespace;
        }
    }

    private async Task<(MemoryDocument, OperationTypeKind)> EvaluateAndUpdateMemoryRecordAsync(string indexName, MemoryRecord record,
        string indexNamespace = "", CancellationToken cancellationToken = default)
    {
        string key = !string.IsNullOrEmpty(record.Key)
            ? record.Key
            : record.Metadata.Id;

        MemoryDocument vectorData = record.ToMemoryDocument();

        MemoryDocument? existingRecord = await _pineconeConnector.FetchVectorsAsync(indexName, new[] { key }, indexNamespace, false, cancellationToken)
            .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);

        if (existingRecord is null)
        {
            return (vectorData, OperationTypeKind.Upsert);
        }

        if (existingRecord.Metadata != null && vectorData.Metadata != null)
        {
            if (existingRecord.Metadata.SequenceEqual(vectorData.Metadata))
            {
                return (vectorData, OperationTypeKind.Skip);
            }
        }

        return (vectorData, OperationTypeKind.Update);
    }
}
