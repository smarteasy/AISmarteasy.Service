using System.Diagnostics.Metrics;
using System.Runtime.CompilerServices;
using AISmarteasy.Core;
using Azure;
using Azure.AI.OpenAI;
using Azure.Core;
using Azure.Core.Pipeline;
using Microsoft.Extensions.Logging;
using OpenAI_API;

namespace AISmarteasy.Service.OpenAI;

public class OpenAITextCompletionConnector : TextCompletionConnector
{
    private const int MAX_RESULTS_PER_PROMPT = 128;
    protected OpenAIClient Client { get; }
    protected OpenAIAPI TtsClient { get; }

    private protected string DeploymentNameOrModelId { get; set; }

    private static readonly Meter Meter = new(typeof(OpenAITextCompletionConnector).Assembly.GetName().Name!);

    private static readonly Counter<int> PromptTokensCounter =
        Meter.CreateCounter<int>(name: "AISmarteasy.Core.Connector.OpenAI.PromptTokens",
            description: "Number of prompt tokens used");

    private static readonly Counter<int> CompletionTokensCounter =
        Meter.CreateCounter<int>(name: "AISmarteasy.Core.Connector.OpenAI.CompletionTokens",
            description: "Number of completion tokens used");

    private static readonly Counter<int> TotalTokensCounter =
        Meter.CreateCounter<int>(name: "AISmarteasy.Core.Connector.OpenAI.TotalTokens",
            description: "Total number of tokens used");

    public OpenAITextCompletionConnector(AIServiceTypeKind serviceType, string apiKey, string? organization = null,
        HttpClient? httpClient = null)
    {
        DeploymentNameOrModelId = OpenAIConfigProvider.ProvideModel(serviceType);

        var options = BuildClientOptions(organization, httpClient);
        Client = new OpenAIClient(apiKey, options);
        TtsClient = new OpenAIAPI(apiKey);
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
            options.AddPolicy(new AddHeaderRequestPolicy("OpenAI-Organization", organization),
                HttpPipelinePosition.PerCall);
        }

        return options;
    }

    public override async Task<ChatHistory> RunAsync(ChatHistory chatHistory, LLMServiceSetting requestSetting, CancellationToken cancellationToken = default)
    {
        Verifier.NotNull(chatHistory);
        Verifier.NotNull(Client);

        ValidateMaxTokens(requestSetting.MaxTokens);


        var chatOptions = CreateCompletionsOptions(requestSetting, chatHistory);

        var responseData = (await RunAsync(() => Client.GetChatCompletionsAsync(chatOptions, cancellationToken)).ConfigureAwait(false)).Value;
        
        CaptureUsageDetails(responseData.Usage);
        if (responseData.Choices.Count == 0)
        {
            throw new CoreException("Chat completions not found");
        }

        var chatResult = responseData.Choices.Select(chatChoice => new ChatResult(responseData, chatChoice)).ToList();
        var assistantContent = chatResult[0].ModelResult.GetResult<ChatModelResult>().Choice.Message.Content;
        chatHistory.AddAssistantMessage(assistantContent);

        return chatHistory;
    }

    public override async IAsyncEnumerable<ChatStreamingResult> RunStreamingAsync(ChatHistory chatHistory,
        LLMServiceSetting requestSetting,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        Verifier.NotNull(chatHistory);
        Verifier.NotNull(Client);

        ValidateMaxTokens(requestSetting.MaxTokens);
        var chatOptions = CreateCompletionsOptions(requestSetting, chatHistory);
        var response = await RunAsync<StreamingResponse<StreamingChatCompletionsUpdate>>(
            () => Client.GetChatCompletionsStreamingAsync(chatOptions, cancellationToken)).ConfigureAwait(false);

        if (response is null)
        {
            throw new CoreException("Chat completions null response");
        }

        var chatStreamingResult = new ChatStreamingResult();

        await foreach (var chatUpdate in response)
        {
            if (chatUpdate.Role.HasValue)
            {
                chatStreamingResult.Role = chatUpdate.Role.Value.ToString();
            }

            if (!string.IsNullOrEmpty(chatUpdate.ContentUpdate))
            {
                chatStreamingResult.Content = chatUpdate.ContentUpdate;
            }

            yield return chatStreamingResult;
        }
    }

    private ChatCompletionsOptions CreateCompletionsOptions(LLMServiceSetting requestSetting, ChatHistory chatHistory)
    {
        if (requestSetting.ResultsPerPrompt is < 1 or > MAX_RESULTS_PER_PROMPT)
        {
            throw new ArgumentOutOfRangeException($"{nameof(requestSetting)}.{nameof(requestSetting.ResultsPerPrompt)}",
                requestSetting.ResultsPerPrompt,
                $"The value must be in range between 1 and {MAX_RESULTS_PER_PROMPT}, inclusive.");
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
            options.Messages.Add(GetRequestMessage(message));
        }

        return options;
    }

    private static ChatRequestMessage GetRequestMessage(ChatMessageContent message)
    {
        if (message.Role == AuthorRole.System)
        {
            return new ChatRequestSystemMessage(message.Content);
        }


        if (message.Role == AuthorRole.User)
        {
            if (message.Items is { Count: > 0 })
            {
                return new ChatRequestUserMessage(message.Items.Select(static item =>
                    (ChatMessageContentItem)(item switch
                    {
                        TextContent textContent => new ChatMessageTextContentItem(textContent.Text),
                        ImageContent imageContent => new ChatMessageImageContentItem(imageContent.Uri),
                        _ => throw new NotSupportedException(
                            $"Unsupported chat message content type '{item.GetType()}'.")
                    })));
            }

            return new ChatRequestUserMessage(message.Content);
        }

        if (message.Role == AuthorRole.Assistant)
        {
            var asstMessage = new ChatRequestAssistantMessage(message.Content);
            return asstMessage;
        }

        throw new NotSupportedException($"Role {message.Role} is not supported.");
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

    protected static async Task<T> RunAsync<T>(Func<Task<T>> request)
    {
        try
        {
            return await request.Invoke().ConfigureAwait(false);
        }
        catch (RequestFailedException e)
        {
            throw e.ToHttpOperationException();
        }
    }

    protected static void ValidateMaxTokens(int? maxTokens)
    {
        if (maxTokens is < 1)
        {
            throw new CoreException($"MaxTokens {maxTokens} is not valid, the value must be greater than zero");
        }
    }
}
