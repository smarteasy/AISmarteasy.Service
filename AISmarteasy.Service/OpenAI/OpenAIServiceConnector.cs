using System.Diagnostics.Metrics;
using AISmarteasy.Core;
using Azure;
using Azure.AI.OpenAI;
using Azure.Core;
using Azure.Core.Pipeline;
using Microsoft.Extensions.Logging;

namespace AISmarteasy.Service.OpenAI;

public class OpenAIServiceConnector : AIServiceConnector
{
    private const int MAX_RESULTS_PER_PROMPT = 128;
    protected OpenAIClient Client { get; }
    private protected string DeploymentNameOrModelId { get; set; }

    private static readonly Meter Meter = new(typeof(OpenAIServiceConnector).Assembly.GetName().Name!);

    private static readonly Counter<int> PromptTokensCounter =
        Meter.CreateCounter<int>(
            name: "AISmarteasy.Core.Connector.OpenAI.PromptTokens",
            description: "Number of prompt tokens used");

    private static readonly Counter<int> CompletionTokensCounter =
        Meter.CreateCounter<int>(
            name: "AISmarteasy.Core.Connector.OpenAI.CompletionTokens",
            description: "Number of completion tokens used");

    private static readonly Counter<int> TotalTokensCounter =
        Meter.CreateCounter<int>(
            name: "AISmarteasy.Core.Connector.OpenAI.TotalTokens",
            description: "Total number of tokens used");

    public OpenAIServiceConnector(AIServiceTypeKind serviceType, string apiKey,
        string? organization = null, HttpClient ? httpClient = null, ILogger? logger = null) 
        : base(logger)
    {
      DeploymentNameOrModelId = OpenAIConfigProvider.ProvideChatCompletionModel();

      var options = BuildClientOptions(organization, httpClient);
      Client = new OpenAIClient(apiKey, options);
    }

    private static OpenAIClientOptions BuildClientOptions(string? organization, HttpClient? httpClient)
    {
        var options = new OpenAIClientOptions
        {
            Diagnostics =
            {
                IsTelemetryEnabled = Telemetry.IsTelemetryEnabled,
                ApplicationId = Telemetry.HTTP_USER_AGENT,
            }
        };

        if (httpClient != null)
        {
            options.Transport = new HttpClientTransport(httpClient);
        }

        if (!string.IsNullOrWhiteSpace(organization))
        {
            options.AddPolicy(new AddHeaderRequestPolicy("OpenAI-Organization", organization), HttpPipelinePosition.PerCall);
        }

        return options;
    }


    public override async Task<ChatHistory> ChatCompletionAsync(ChatHistory chatHistory, LLMServiceSetting requestSetting, CancellationToken cancellationToken = default)
    {
        Verifier.NotNull(chatHistory);
        Verifier.NotNull(Client);

        ValidateMaxTokens(requestSetting.MaxTokens);
        var chatOptions = CreateCompletionsOptions(requestSetting, chatHistory);

        Response<ChatCompletions>? response = await RunRequestAsync<Response<ChatCompletions>?>(
            () => Client.GetChatCompletionsAsync(chatOptions, cancellationToken)).ConfigureAwait(false);

        if (response is null)
        {
            throw new CoreException("Chat completions null response");
        }

        var responseData = response.Value;

        if (responseData.Choices.Count == 0)
        {
            throw new CoreException("Chat completions not found");
        }

        CaptureUsageDetails(responseData.Usage);

        var chatResult = responseData.Choices.Select(chatChoice => new ChatResult(responseData, chatChoice)).ToList();
        var assistantContent = chatResult[0].ModelResult.GetResult<ChatModelResult>().Choice.Message.Content;
        chatHistory.AddAssistantMessage(assistantContent);

        return chatHistory;
    }


    private ChatCompletionsOptions CreateCompletionsOptions(LLMServiceSetting requestSetting, IEnumerable<ChatMessageBase> chatHistory)
    {
        if (requestSetting.ResultsPerPrompt is < 1 or > MAX_RESULTS_PER_PROMPT)
        {
            throw new ArgumentOutOfRangeException($"{nameof(requestSetting)}.{nameof(requestSetting.ResultsPerPrompt)}", requestSetting.ResultsPerPrompt, $"The value must be in range between 1 and {MAX_RESULTS_PER_PROMPT}, inclusive.");
        }

        var options = new ChatCompletionsOptions
        {
            MaxTokens = requestSetting.MaxTokens,
            Temperature = (float?)requestSetting.Temperature,
            NucleusSamplingFactor = (float?)requestSetting.TopP,
            FrequencyPenalty = (float?)requestSetting.FrequencyPenalty,
            PresencePenalty = (float?)requestSetting.PresencePenalty,
            ChoiceCount = requestSetting.ResultsPerPrompt,
            DeploymentName = DeploymentNameOrModelId
        };

        if (requestSetting.TokenSelectionBiases != null)
        {
            foreach (var keyValue in requestSetting.TokenSelectionBiases)
            {
                options.TokenSelectionBiases.Add(keyValue.Key, keyValue.Value);
            }
        }


        if (requestSetting.StopSequences is { Count: > 0 })
        {
            foreach (var s in requestSetting.StopSequences)
            {
                options.StopSequences.Add(s);
            }
        }
        
        foreach (var message in chatHistory)
        {
            var role = GetValidChatRole(message.Role);
            options.Messages.Add(ChatMessageConverter.Convert(role, message.Content));
        }

        return options;
    }

    private static ChatRole GetValidChatRole(AuthorRole role)
    {
        var validRole = new ChatRole(role.Label);

        if (validRole != ChatRole.User &&
            validRole != ChatRole.System &&
            validRole != ChatRole.Assistant)
        {
            throw new ArgumentException($"Invalid chat message author role: {role}");
        }

        return validRole;
    }

    private void CaptureUsageDetails(CompletionsUsage usage)
    {
        Logger.LogInformation(
            "Prompt tokens: {PromptTokens}. Completion tokens: {CompletionTokens}. Total tokens: {TotalTokens}.",
            usage.PromptTokens, usage.CompletionTokens, usage.TotalTokens);

        PromptTokensCounter.Add(usage.PromptTokens);
        CompletionTokensCounter.Add(usage.CompletionTokens);
        TotalTokensCounter.Add(usage.TotalTokens);
    }

    //private protected async Task<SemanticAnswer> GetTextResultsAsync(string prompt, AIRequestSettings requestSettings,
    //    CancellationToken cancellationToken = default)
    //{
    //    Verify.NotNull(requestSettings);
    //    ValidateMaxTokens(requestSettings.MaxTokens);
    //    Verify.NotNull(Client);

    //    var options = CreateCompletionsOptions(prompt, requestSettings);

    //    Response<Completions>? response = await RunRequestAsync<Response<Completions>?>(
    //        () => Client.GetCompletionsAsync(ModelId, options, cancellationToken)).ConfigureAwait(false);

    //    Verify.NotNull(response);

    //    var responseData = response.Value;

    //    if (responseData.Choices.Count == 0)
    //    {
    //        throw new SKException("Text completions not found");
    //    }

    //    CaptureUsageDetails(responseData.Usage);

    //    return new SemanticAnswer(responseData.Choices[0].Text);
    //}



    //private protected async Task<IList<ReadOnlyMemory<float>>> GetEmbeddingsAsync(IList<string> texts, CancellationToken cancellationToken = default)
    //{
    //    Verify.NotNull(Client);

    //    var result = new List<ReadOnlyMemory<float>>(texts.Count);
    //    foreach (var text in texts)
    //    {
    //        var options = new EmbeddingsOptions(text);

    //        Response<Embeddings>? response = await RunRequestAsync<Response<Embeddings>?>(
    //            () => Client.GetEmbeddingsAsync(ModelId, options, cancellationToken)).ConfigureAwait(false);

    //        if (response is null)
    //        {
    //            throw new SKException("Text embedding null response");
    //        }

    //        if (response.Value.Data.Count == 0)
    //        {
    //            throw new SKException("Text embedding not found");
    //        }

    //        result.Add(response.Value.Data[0].Embedding.ToArray());
    //    }

    //    return result;
    //}

    //private protected async IAsyncEnumerable<IChatStreamingResult> GetChatStreamingResultsAsync(IEnumerable<ChatMessageBase> chat,
    //    AIRequestSettings? requestSettings, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    //{
    //    Verify.NotNull(chat);
    //    Verify.NotNull(Client);
    //    requestSettings ??= new();

    //    ValidateMaxTokens(requestSettings.MaxTokens);

    //    var options = CreateChatCompletionsOptions(requestSettings, chat);

    //    Response<StreamingChatCompletions>? response = await RunRequestAsync<Response<StreamingChatCompletions>>(
    //        () => Client.GetChatCompletionsStreamingAsync(ModelId, options, cancellationToken)).ConfigureAwait(false);

    //    if (response is null)
    //    {
    //        throw new SKException("Chat completions null response");
    //    }

    //    using StreamingChatCompletions streamingChatCompletions = response.Value;
    //    await foreach (StreamingChatChoice choice in streamingChatCompletions.GetChoicesStreaming(cancellationToken).ConfigureAwait(false))
    //    {
    //        yield return new ChatStreamingResult(response.Value, choice);
    //    }
    //}

    //private protected async Task<IReadOnlyList<ITextResult>> GetChatResultsAsTextAsync(string text,
    //    AIRequestSettings? textSettings, CancellationToken cancellationToken = default)
    //{
    //    textSettings ??= new();
    //    ChatHistory chat = PrepareChatHistory(text, textSettings, out AIRequestSettings chatSettings);

    //    return (await GetChatResultsAsync(chat, chatSettings, cancellationToken).ConfigureAwait(false))
    //        .OfType<ITextResult>()
    //        .ToList();
    //}

    //private protected async IAsyncEnumerable<ITextStreamingResult> GetChatStreamingResultsAsTextAsync(string text, AIRequestSettings? requestSettings,
    //    [EnumeratorCancellation] CancellationToken cancellationToken = default)
    //{
    //    ChatHistory chat = PrepareChatHistory(text, requestSettings, out AIRequestSettings settings);

    //    await foreach (var chatCompletionStreamingResult in GetChatStreamingResultsAsync(chat, settings, cancellationToken).ConfigureAwait(false))
    //    {
    //        yield return (ITextStreamingResult)chatCompletionStreamingResult;
    //    }
    //}

    //protected abstract ChatHistory PrepareChatHistory(string text, AIRequestSettings? requestSettings, out AIRequestSettings settings);






    //protected static void ValidateMaxTokens(int? maxTokens)
    //{
    //    if (maxTokens is < 1)
    //    {
    //        throw new SKException($"MaxTokens {maxTokens} is not valid, the value must be greater than zero");
    //    }
    //}

    //protected static async Task<T> RunRequestAsync<T>(Func<Task<T>?> request)
    //{
    //    try
    //    {
    //        return await request.Invoke()!.ConfigureAwait(false);
    //    }
    //    catch (RequestFailedException e)
    //    {
    //        throw e.ToHttpOperationException();
    //    }
    //}

    //public ChatHistory CreateNewChat(string? systemMessage = null)
    //{
    //    return new OpenAIChatHistory(systemMessage);
    //}


}
