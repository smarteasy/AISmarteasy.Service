using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Numerics.Tensors;
using System.Runtime.CompilerServices;
using AISmarteasy.Core;

namespace AISmarteasy.Service;

public class VolatileMemoryStore : IMemoryStore
{
    private readonly ConcurrentDictionary<string,
        ConcurrentDictionary<string, MemoryRecord>> _store = new();

    public Task CreateCollectionAsync(string collectionName, CancellationToken cancellationToken = default)
    {
        Verifier.NotNullOrWhitespace(collectionName);

        _store.TryAdd(collectionName, new ConcurrentDictionary<string, MemoryRecord>());
        return Task.CompletedTask;
    }

    public Task<bool> DoesCollectionExistAsync(string collectionName, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_store.ContainsKey(collectionName));
    }

    public IAsyncEnumerable<string> GetCollectionsAsync(CancellationToken cancellationToken = default)
    {
        return _store.Keys.ToAsyncEnumerable();
    }

    public Task DeleteCollectionAsync(string collectionName, CancellationToken cancellationToken = default)
    {
        if (!_store.TryRemove(collectionName, out _))
        {
            return Task.FromException(new CoreException($"Could not delete collection {collectionName}"));
        }

        return Task.CompletedTask;
    }

    public Task<string> UpsertAsync(string collectionName, MemoryRecord record, CancellationToken cancellationToken = default)
    {
        Verifier.NotNull(record);
        Verifier.NotNull(record.Metadata.Id);

        if (TryGetCollection(collectionName, out var collectionDict, create: false))
        {
            record.Key = record.Metadata.Id;
            collectionDict[record.Key] = record;
        }
        else
        {
            return Task.FromException<string>(new CoreException($"Attempted to access a memory collection that does not exist: {collectionName}"));
        }

        return Task.FromResult(record.Key);
    }

    public async IAsyncEnumerable<string> UpsertBatchAsync(string collectionName, IEnumerable<MemoryRecord> records,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        foreach (var r in records)
        {
            yield return await UpsertAsync(collectionName, r, cancellationToken).ConfigureAwait(false);
        }
    }

    public Task<MemoryRecord?> GetAsync(string collectionName, string key,CancellationToken cancellationToken = default)
    {
        if (TryGetCollection(collectionName, out var collectionDict)
            && collectionDict.TryGetValue(key, out var dataEntry))
        {
            return Task.FromResult<MemoryRecord?>(dataEntry);
        }

        return Task.FromResult<MemoryRecord?>(null);
    }

    public async IAsyncEnumerable<MemoryRecord> GetBatchAsync(string collectionName, IEnumerable<string> keys, 
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        foreach (var key in keys)
        {
            var record = await GetAsync(collectionName, key, cancellationToken).ConfigureAwait(false);

            if (record != null)
            {
                yield return record;
            }
        }
    }

    public Task RemoveAsync(string collectionName, string key, CancellationToken cancellationToken = default)
    {
        if (TryGetCollection(collectionName, out var collectionDict))
        {
            collectionDict.TryRemove(key, out _);
        }

        return Task.CompletedTask;
    }

    public Task RemoveBatchAsync(string collectionName, IEnumerable<string> keys, CancellationToken cancellationToken = default)
    {
        return Task.WhenAll(keys.Select(key => RemoveAsync(collectionName, key, cancellationToken)));
    }

    public IAsyncEnumerable<(MemoryRecord, double)> GetNearestMatchesAsync(string collectionName, string collectionNamespace, ReadOnlyMemory<float> embedding, int topK,
        double minRelevanceScore = 0.0, bool isIncludeMetadata = true, CancellationToken cancellationToken = default)
    {
        if (topK <= 0)
        {
            return AsyncEnumerableExtensions.Empty<(MemoryRecord, double)>();
        }

        ICollection<MemoryRecord>? embeddingCollection = null;
        if (TryGetCollection(collectionName, out var collectionDict))
        {
            embeddingCollection = collectionDict.Values;
        }

        if (embeddingCollection == null || embeddingCollection.Count == 0)
        {
            return AsyncEnumerableExtensions.Empty<(MemoryRecord, double)>();
        }

        TopNCollection<MemoryRecord> embeddings = new(topK);

        foreach (var record in embeddingCollection)
        {
            {
                double similarity = TensorPrimitives.CosineSimilarity(embedding.Span, record.Embedding.Span);
                if (similarity >= minRelevanceScore)
                {
                    var entry = record;
                    embeddings.Add(new(entry, similarity));
                }
            }
        }

        embeddings.SortByScore();

        return embeddings.Select(x => (x.Value, x.Score)).ToAsyncEnumerable();
    }

    protected bool TryGetCollection(string name, 
        [NotNullWhen(true)] out ConcurrentDictionary<string, MemoryRecord>? collection, bool create = false)
    {
        if (_store.TryGetValue(name, out collection))
        {
            return true;
        }

        if (create)
        {
            collection = new ConcurrentDictionary<string, MemoryRecord>();
            return _store.TryAdd(name, collection);
        }

        collection = null;
        return false;
    }
}
