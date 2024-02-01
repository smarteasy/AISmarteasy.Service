using AISmarteasy.Core;

namespace AISmarteasy.Service.Pinecone;

public interface IPineconeMemoryStore : IMemoryStore
{
    Task<string> UpsertToNamespaceAsync(string indexName, string indexNamespace, MemoryRecord record, CancellationToken cancellationToken);

    IAsyncEnumerable<string> UpsertBatchToNamespaceAsync(string indexName, string indexNamespace, IEnumerable<MemoryRecord> records, CancellationToken cancellationToken );

    Task<MemoryRecord?> GetFromNamespaceAsync(string indexName, string indexNamespace, string key, CancellationToken cancellationToken);

    IAsyncEnumerable<MemoryRecord> GetBatchFromNamespaceAsync(string indexName, string indexNamespace, IEnumerable<string> keys, CancellationToken cancellationToken);

    IAsyncEnumerable<MemoryRecord?> GetWithDocumentIdAsync(string indexName, string documentId, int topK, string indexNamespace, CancellationToken cancellationToken);

    IAsyncEnumerable<MemoryRecord?> GetWithDocumentIdBatchAsync(string indexName, IEnumerable<string> documentIds, int topK, string indexNamespace, CancellationToken cancellationToken);

    public IAsyncEnumerable<MemoryRecord?> GetBatchWithFilterAsync(string indexName, Dictionary<string, object> filter, 
        int topK, string indexNamespace,CancellationToken cancellationToken);

    Task RemoveFromNamespaceAsync(string indexName, string indexNamespace, string key, CancellationToken cancellationToken);

    Task RemoveBatchFromNamespaceAsync(string indexName, string indexNamespace, IEnumerable<string> keys, CancellationToken cancellationToken);

    Task RemoveWithDocumentIdAsync(string indexName, string documentId, string indexNamespace, CancellationToken cancellationToken);

    public Task RemoveWithDocumentIdBatchAsync(string indexName, IEnumerable<string> documentIds, string indexNamespace, CancellationToken cancellationToken);

    Task RemoveWithFilterAsync(string indexName, Dictionary<string, object> filter, string indexNamespace, CancellationToken cancellationToken);

    Task ClearNamespaceAsync(string indexName, string indexNamespace, CancellationToken cancellationToken);

    IAsyncEnumerable<string?> ListNamespacesAsync(string indexName, CancellationToken cancellationToken);
}
