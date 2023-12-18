using Azure.AI.OpenAI;

namespace AISmarteasy.Service.OpenAI;

public class ChatModelResult
{
    public string Id { get; }

    public DateTimeOffset Created { get; }

    public IReadOnlyList<ContentFilterResultsForPrompt> PromptFilterResults { get; }

    public ChatChoice Choice { get; }

    public CompletionsUsage Usage { get; }

    internal ChatModelResult(ChatCompletions completionsData, ChatChoice choiceData)
    {
        Id = completionsData.Id;
        Created = completionsData.Created;
        PromptFilterResults = completionsData.PromptFilterResults;
        Choice = choiceData;
        Usage = completionsData.Usage;
    }
}
