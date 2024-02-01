using AISmarteasy.Core;

namespace AISmarteasy.Service;

public class MemoryChunkingStrategist  
{
    private readonly TextPartitioningOptions _options;
    private readonly TextChunker.TokenCounter _tokenCounter;
    //private readonly int _maxTokensPerPartition = int.MaxValue;

    public MemoryChunkingStrategist(TextPartitioningOptions? options = null)
    {
        _options = options ?? new TextPartitioningOptions();
        _options.Validate();
        _tokenCounter = DefaultGptTokenizer.StaticCountTokens;
    }

    public bool SplitAsync(string text, CancellationToken cancellationToken = default)
    {
        var lines = TextChunker.SplitPlainTextLines(text, maxTokensPerLine: _options.MaxTokensPerLine, tokenCounter: _tokenCounter);
        var paragraphs = TextChunker.SplitPlainTextParagraphs(lines, _options.MaxTokensPerParagraph, _options.OverlappingTokens, tokenCounter: _tokenCounter);


        //var text = new MsWordDecoder().DocToText("mswordfile.docx");
        //text = new MsExcelDecoder().DocToText("msexcelfile.xlsx");
        //text = new MsPowerPointDecoder().DocToText("mspowerpointfile.pptx",
        //    withSlideNumber: true,
        //    withEndOfSlideMarker: false,
        //    skipHiddenSlides: true);
        //var pages = new PdfDecoder().DocToText("file1.pdf");
        //foreach (var page in pages)
        //{
        //    Console.WriteLine(page.Text);
        //}


        //BinaryData partitionContent = await this._orchestrator.ReadFileAsync(pipeline, file.Name, cancellationToken).ConfigureAwait(false);
        // Skip empty partitions. Also: partitionContent.ToString() throws an exception if there are no bytes.
        //if (partitionContent.ToArray().Length == 0) { continue; }
        //foreach (DataPipeline.FileDetails uploadedFile in pipeline.Files)
        //{
        //    // Track new files being generated (cannot edit originalFile.GeneratedFiles while looping it)
        //    Dictionary<string, DataPipeline.GeneratedFileDetails> newFiles = new();
        //    foreach (KeyValuePair<string, DataPipeline.GeneratedFileDetails> generatedFile in uploadedFile.GeneratedFiles)
        //    {
        //        var file = generatedFile.Value;
        //        if (file.AlreadyProcessedBy(this))
        //        {
        //            this._log.LogTrace("File {0} already processed by this handler", file.Name);
        //            continue;
        //        }
        //        // Partition only the original text
        //        if (file.ArtifactType != DataPipeline.ArtifactTypes.ExtractedText)
        //        {
        //            this._log.LogTrace("Skipping file {0} (not original text)", file.Name);
        //            continue;
        //        }
        //        switch (file.MimeType)
        //        {
        //            case MimeTypes.PlainText:
        //            {
        //                string content = partitionContent.ToString();
        //                lines = TextChunker.SplitPlainTextLines(content, maxTokensPerLine: this._options.MaxTokensPerLine, tokenCounter: this._tokenCounter);
        //                paragraphs = TextChunker.SplitPlainTextParagraphs(
        //                    lines, maxTokensPerParagraph: this._options.MaxTokensPerParagraph, overlapTokens: this._options.OverlappingTokens, tokenCounter: this._tokenCounter);
        //                break;
        //            }
        //            case MimeTypes.MarkDown:
        //            {
        //                this._log.LogDebug("Partitioning MarkDown file {0}", file.Name);
        //                string content = partitionContent.ToString();
        //                lines = TextChunker.SplitMarkDownLines(content, maxTokensPerLine: this._options.MaxTokensPerLine, tokenCounter: this._tokenCounter);
        //                paragraphs = TextChunker.SplitMarkdownParagraphs(
        //                    lines, maxTokensPerParagraph: this._options.MaxTokensPerParagraph, overlapTokens: this._options.OverlappingTokens, tokenCounter: this._tokenCounter);
        //                break;
        //            }
        //            default:
        //                this._log.LogWarning("File {0} cannot be partitioned, type '{1}' not supported", file.Name, file.MimeType);
        //                // Don't partition other files
        //                continue;
        //        }
        //        if (paragraphs.Count == 0) { continue; }
        //        this._log.LogDebug("Saving {0} file partitions", paragraphs.Count);
        //        for (int index = 0; index < paragraphs.Count; index++)
        //        {
        //            string text = paragraphs[index];
        //            BinaryData textData = new(text);
        //            int tokenCount = this._tokenCounter(text);
        //            this._log.LogDebug("Partition size: {0} tokens", tokenCount);
        //            var destFile = uploadedFile.GetPartitionFileName(index);
        //            await this._orchestrator.WriteFileAsync(pipeline, destFile, textData, cancellationToken).ConfigureAwait(false);
        //            var destFileDetails = new DataPipeline.GeneratedFileDetails
        //            {
        //                Id = Guid.NewGuid().ToString("N"),
        //                ParentId = uploadedFile.Id,
        //                Name = destFile,
        //                Size = text.Length,
        //                MimeType = MimeTypes.PlainText,
        //                ArtifactType = DataPipeline.ArtifactTypes.TextPartition,
        //                Tags = pipeline.Tags,
        //                ContentSHA256 = textData.CalculateSHA256(),
        //            };
        //            newFiles.Add(destFile, destFileDetails);
        //            destFileDetails.MarkProcessedBy(this);
        //        }
        //        file.MarkProcessedBy(this);
        //    }
        //    // Add new files to pipeline status
        //    foreach (var file in newFiles)
        //    {
        //        uploadedFile.GeneratedFiles.Add(file.Key, file.Value);
        //    }
        //}
        return true;
    }
}
