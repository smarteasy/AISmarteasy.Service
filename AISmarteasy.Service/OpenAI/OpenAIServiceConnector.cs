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

    public OpenAIServiceConnector(AIServiceTypeKind serviceType, string apiKey,
        string? organization = null, HttpClient ? httpClient = null, ILogger? logger = null) 
        : base(logger)
    {
      DeploymentNameOrModelId = OpenAIConfigProvider.ProvideCompletionModel();

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

    public override async Task<string> TextCompletionAsync(string prompt, LLMServiceSetting requestSetting, CancellationToken cancellationToken = default)
    {
        Verifier.NotNull(requestSetting);
        Verifier.NotNull(Client);
        ValidateMaxTokens(requestSetting.MaxTokens);


        var options = CreateCompletionsOptions(prompt, requestSetting);
        
        Response<Completions>? response = await RunRequestAsync<Response<Completions>?>(
            () => Client.GetCompletionsAsync(options, cancellationToken)).ConfigureAwait(false);

        Verifier.NotNull(response);

        var responseData = response.Value;

        if (responseData.Choices.Count == 0)
        {
            throw new CoreException("Text completions not found");
        }

        return responseData.Choices[0].Text;
    }

    private CompletionsOptions CreateCompletionsOptions(string prompt, LLMServiceSetting requestSetting)
    {
        if (requestSetting.ResultsPerPrompt is < 1 or > MAX_RESULTS_PER_PROMPT)
        {
            throw new ArgumentOutOfRangeException($"{nameof(requestSetting)}.{nameof(requestSetting.ResultsPerPrompt)}", requestSetting.ResultsPerPrompt, 
                $"The value must be in range between 1 and {MAX_RESULTS_PER_PROMPT}, inclusive.");
        }

        var options = new CompletionsOptions
        {
            Prompts = { prompt.Replace("\r\n", "\n") },
            MaxTokens = requestSetting.MaxTokens,
            Temperature = (float?)requestSetting.Temperature,
            NucleusSamplingFactor = (float?)requestSetting.TopP,
            FrequencyPenalty = (float?)requestSetting.FrequencyPenalty,
            PresencePenalty = (float?)requestSetting.PresencePenalty,
            Echo = false,
            ChoicesPerPrompt = requestSetting.ResultsPerPrompt,
            GenerationSampleCount = requestSetting.ResultsPerPrompt,
            LogProbabilityCount = null,
            User = null,
            DeploymentName = DeploymentNameOrModelId
        };

        if (requestSetting.StopSequences is { Count: > 0 })
        {
            foreach (var s in requestSetting.StopSequences)
            {
                options.StopSequences.Add(s);
            }
        }

        if (requestSetting.TokenSelectionBiases is not null)
        {
            foreach (var keyValue in requestSetting.TokenSelectionBiases)
            {
                options.TokenSelectionBiases.Add(keyValue.Key, keyValue.Value);
            }
        }

        return options;
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

    //private protected async Task<IReadOnlyList<IChatResult>> GetChatResultsAsync(ChatHistory chatHistory, AIRequestSettings chatSettings, 
    //    CancellationToken cancellationToken = default)
    //{
    //    Verify.NotNull(chatHistory);
    //    Verify.NotNull(Client);

    //    ValidateMaxTokens(chatSettings.MaxTokens);
    //    var chatOptions = CreateChatCompletionsOptions(chatSettings, chatHistory);

    //    Response<ChatCompletions>? response = await RunRequestAsync<Response<ChatCompletions>?>(
    //        () => Client.GetChatCompletionsAsync(ModelId, chatOptions, cancellationToken)).ConfigureAwait(false);

    //    if (response is null)
    //    {
    //        throw new SKException("Chat completions null response");
    //    }

    //    var responseData = response.Value;

    //    if (responseData.Choices.Count == 0)
    //    {
    //        throw new SKException("Chat completions not found");
    //    }

    //    CaptureUsageDetails(responseData.Usage);

    //    return responseData.Choices.Select(chatChoice => new ChatResult(responseData, chatChoice)).ToList();
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

    //protected static CompletionsOptions CreateCompletionsOptions(string text, AIRequestSettings requestSettings)
    //{
    //    if (requestSettings.ResultsPerPrompt is < 1 or > MAX_RESULTS_PER_PROMPT)
    //    {
    //        throw new ArgumentOutOfRangeException($"{nameof(requestSettings)}.{nameof(requestSettings.ResultsPerPrompt)}", requestSettings.ResultsPerPrompt, $"The value must be in range between 1 and {MAX_RESULTS_PER_PROMPT}, inclusive.");
    //    }

    //    var options = new CompletionsOptions
    //    {
    //        Prompts = { text.NormalizeLineEndings() },
    //        MaxTokens = requestSettings.MaxTokens,
    //        Temperature = (float?)requestSettings.Temperature,
    //        NucleusSamplingFactor = (float?)requestSettings.TopP,
    //        FrequencyPenalty = (float?)requestSettings.FrequencyPenalty,
    //        PresencePenalty = (float?)requestSettings.PresencePenalty,
    //        Echo = false,
    //        ChoicesPerPrompt = requestSettings.ResultsPerPrompt,
    //        GenerationSampleCount = requestSettings.ResultsPerPrompt,
    //        LogProbabilityCount = null,
    //        User = null,
    //    };

    //    foreach (var keyValue in requestSettings.TokenSelectionBiases)
    //    {
    //        options.TokenSelectionBiases.Add(keyValue.Key, keyValue.Value);
    //    }

    //    if (requestSettings.StopSequences is { Count: > 0 })
    //    {
    //        foreach (var s in requestSettings.StopSequences)
    //        {
    //            options.StopSequences.Add(s);
    //        }
    //    }

    //    return options;
    //}

    //private static ChatCompletionsOptions CreateChatCompletionsOptions(AIRequestSettings requestSettings, IEnumerable<ChatMessageBase> chatHistory)
    //{
    //    if (requestSettings.ResultsPerPrompt is < 1 or > MAX_RESULTS_PER_PROMPT)
    //    {
    //        throw new ArgumentOutOfRangeException($"{nameof(requestSettings)}.{nameof(requestSettings.ResultsPerPrompt)}", requestSettings.ResultsPerPrompt, $"The value must be in range between 1 and {MAX_RESULTS_PER_PROMPT}, inclusive.");
    //    }

    //    var options = new ChatCompletionsOptions
    //    {
    //        MaxTokens = requestSettings.MaxTokens,
    //        Temperature = (float?)requestSettings.Temperature,
    //        NucleusSamplingFactor = (float?)requestSettings.TopP,
    //        FrequencyPenalty = (float?)requestSettings.FrequencyPenalty,
    //        PresencePenalty = (float?)requestSettings.PresencePenalty,
    //        ChoiceCount = requestSettings.ResultsPerPrompt
    //    };

    //    foreach (var keyValue in requestSettings.TokenSelectionBiases)
    //    {
    //        options.TokenSelectionBiases.Add(keyValue.Key, keyValue.Value);
    //    }

    //    if (requestSettings.StopSequences is { Count: > 0 })
    //    {
    //        foreach (var s in requestSettings.StopSequences)
    //        {
    //            options.StopSequences.Add(s);
    //        }
    //    }

    //    foreach (var message in chatHistory)
    //    {
    //        var validRole = GetValidChatRole(message.Role);
    //        options.Messages.Add(new ChatMessage(validRole, message.Content));
    //    }

    //    return options;
    //}

    //private static ChatRole GetValidChatRole(AuthorRole role)
    //{
    //    var validRole = new ChatRole(role.Label);

    //    if (validRole != ChatRole.User &&
    //        validRole != ChatRole.System &&
    //        validRole != ChatRole.Assistant)
    //    {
    //        throw new ArgumentException($"Invalid chat message author role: {role}");
    //    }

    //    return validRole;
    //}

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

    //private void CaptureUsageDetails(CompletionsUsage usage)
    //{
    //    Logger.LogInformation(
    //        "Prompt tokens: {PromptTokens}. Completion tokens: {CompletionTokens}. Total tokens: {TotalTokens}.",
    //        usage.PromptTokens, usage.CompletionTokens, usage.TotalTokens);

    //    PromptTokensCounter.Add(usage.PromptTokens);
    //    CompletionTokensCounter.Add(usage.CompletionTokens);
    //    TotalTokensCounter.Add(usage.TotalTokens);
    //}
}
