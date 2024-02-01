using AISmarteasy.Core;

namespace AISmarteasy.Service;

public class ServerlessMemory : IMemory
{
    //private readonly ISearchClient _searchClient;
    //private readonly InProcessPipelineOrchestrator _orchestrator;

    //public InProcessPipelineOrchestrator Orchestrator => this._orchestrator;

    //public ServerlessMemory(
    //    InProcessPipelineOrchestrator orchestrator,
    //    ISearchClient searchClient)
    //{
    //    this._orchestrator = orchestrator ?? throw new ConfigurationException("The orchestrator is NULL");
    //    this._searchClient = searchClient ?? throw new ConfigurationException("The search client is NULL");
    //}

    ///// <summary>
    ///// Register a pipeline handler. If a handler for the same step name already exists, it gets replaced.
    ///// </summary>
    ///// <param name="handler">Handler instance</param>
    //public void AddHandler(IPipelineStepHandler handler)
    //{
    //    this._orchestrator.AddHandler(handler);
    //}

    //public Task<string> ImportDocumentAsync(
    //    Document document,
    //    string? index = null,
    //    IEnumerable<string>? steps = null,
    //    CancellationToken cancellationToken = default)
    //{
    //    DocumentUploadRequest uploadRequest = new(document, index, steps);
    //    return this.ImportDocumentAsync(uploadRequest, cancellationToken);
    //}

    public bool ImportAsync(DocumentRequest request, CancellationToken cancellationToken = default)
    {
        var document = new MemorySourceDocument(request.DocumentId, tags: request.Tags).AddFile(request.DocumentPath);
        var importRequest = new MemoryDocumentImportRequest(document, request.Index);
        var text = ImportDocumentAsync(importRequest, cancellationToken);
        var splitRequest = new MemoryDocumentSplitRequest(text);
        SplitAsync(splitRequest, cancellationToken);
        
        return true;
    }

    public string ImportDocumentAsync(MemoryDocumentImportRequest request, CancellationToken cancellationToken = default)
    {
        //var index = request.Index.CleanName();
        var text = new MemoryDocumentHandler().DocToText(request.Files[0].FileName);
        return text;
    }

    public bool SplitAsync(MemoryDocumentSplitRequest request, CancellationToken cancellationToken = default)
    {
        return new MemoryChunkingStrategist().SplitAsync(request.Text, cancellationToken);
    }


    ///// <inheritdoc />
    //public async Task<string> ImportTextAsync(
    //    string text,
    //    string? documentId = null,
    //    TagCollection? tags = null,
    //    string? index = null,
    //    IEnumerable<string>? steps = null,
    //    CancellationToken cancellationToken = default)
    //{
    //    var content = new MemoryStream(Encoding.UTF8.GetBytes(text));
    //    await using (content.ConfigureAwait(false))
    //    {
    //        return await this.ImportDocumentAsync(
    //            content,
    //            fileName: "content.txt",
    //            documentId: documentId,
    //            tags: tags,
    //            index: index,
    //            steps: steps,
    //            cancellationToken: cancellationToken).ConfigureAwait(false);
    //    }
    //}

    ///// <inheritdoc />
    //public Task<string> ImportDocumentAsync(
    //    Stream content,
    //    string? fileName = null,
    //    string? documentId = null,
    //    TagCollection? tags = null,
    //    string? index = null,
    //    IEnumerable<string>? steps = null,
    //    CancellationToken cancellationToken = default)
    //{
    //    var document = new Document(documentId, tags: tags).AddStream(fileName, content);
    //    DocumentUploadRequest uploadRequest = new(document, index, steps);
    //    return this.ImportDocumentAsync(uploadRequest, cancellationToken);
    //}


    ///// <inheritdoc />
    //public async Task<string> ImportWebPageAsync(
    //    string url,
    //    string? documentId = null,
    //    TagCollection? tags = null,
    //    string? index = null,
    //    IEnumerable<string>? steps = null,
    //    CancellationToken cancellationToken = default)
    //{
    //    var uri = new Uri(url);
    //    Verify.ValidateUrl(uri.AbsoluteUri, requireHttps: false, allowReservedIp: false, allowQuery: true);

    //    Stream content = new MemoryStream(Encoding.UTF8.GetBytes(uri.AbsoluteUri));
    //    await using (content.ConfigureAwait(false))
    //    {
    //        return await this.ImportDocumentAsync(content, fileName: "content.url", documentId: documentId, tags: tags, index: index, steps: steps, cancellationToken: cancellationToken)
    //            .ConfigureAwait(false);
    //    }
    //}

    ///// <inheritdoc />
    //public async Task<IEnumerable<IndexDetails>> ListIndexesAsync(CancellationToken cancellationToken = default)
    //{
    //    return (from index in await this._searchClient.ListIndexesAsync(cancellationToken).ConfigureAwait(false)
    //            select new IndexDetails { Name = index });
    //}

    ///// <inheritdoc />
    //public Task DeleteIndexAsync(string? index = null, CancellationToken cancellationToken = default)
    //{
    //    return this._orchestrator.StartIndexDeletionAsync(index: index, cancellationToken);
    //}

    ///// <inheritdoc />
    //public Task DeleteDocumentAsync(string documentId, string? index = null, CancellationToken cancellationToken = default)
    //{
    //    return this._orchestrator.StartDocumentDeletionAsync(documentId: documentId, index: index, cancellationToken);
    //}


    ///// <inheritdoc />
    //public Task<SearchResult> SearchAsync(
    //    string query,
    //    string? index = null,
    //    MemoryFilter? filter = null,
    //    ICollection<MemoryFilter>? filters = null,
    //    double minRelevance = 0,
    //    int limit = -1,
    //    CancellationToken cancellationToken = default)
    //{
    //    if (filter != null)
    //    {
    //        if (filters == null) { filters = new List<MemoryFilter>(); }

    //        filters.Add(filter);
    //    }

    //    index = IndexExtensions.CleanName(index);
    //    return this._searchClient.SearchAsync(
    //        index: index,
    //        query: query,
    //        filters: filters,
    //        minRelevance: minRelevance,
    //        limit: limit,
    //        cancellationToken: cancellationToken);
    //}

    ///// <inheritdoc />
    //public Task<MemoryAnswer> AskAsync(
    //    string question,
    //    string? index = null,
    //    MemoryFilter? filter = null,
    //    ICollection<MemoryFilter>? filters = null,
    //    double minRelevance = 0,
    //    CancellationToken cancellationToken = default)
    //{
    //    if (filter != null)
    //    {
    //        if (filters == null) { filters = new List<MemoryFilter>(); }

    //        filters.Add(filter);
    //    }

    //    index = IndexExtensions.CleanName(index);
    //    return this._searchClient.AskAsync(
    //        index: index,
    //        question: question,
    //        filters: filters,
    //        minRelevance: minRelevance,
    //        cancellationToken: cancellationToken);
    //}
}
