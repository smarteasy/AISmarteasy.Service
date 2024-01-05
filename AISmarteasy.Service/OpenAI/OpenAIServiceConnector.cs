using System.Diagnostics.Metrics;
using System.Runtime.CompilerServices;
using System.Text;
using AISmarteasy.Core;
using AISmarteasy.Core.Function;
using Azure;
using Azure.AI.OpenAI;
using Azure.Core;
using Azure.Core.Pipeline;
using Microsoft.Extensions.Logging;
using OpenAI_API;
using OpenAI_API.Audio;
using static OpenAI_API.Audio.TextToSpeechRequest;

namespace AISmarteasy.Service.OpenAI;

public class OpenAIServiceConnector : AIServiceConnector
{
    private const int MAX_RESULTS_PER_PROMPT = 128;
    protected OpenAIClient Client { get; }
    protected OpenAIAPI TtsClient { get; }

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



    public OpenAIServiceConnector(AIServiceTypeKind serviceType, string apiKey, ILogger logger,
        string? organization = null, HttpClient ? httpClient = null) 
        : base(logger)
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
            options.AddPolicy(new AddHeaderRequestPolicy("OpenAI-Organization", organization), HttpPipelinePosition.PerCall);
        }

        return options;
    }


    public override async Task<ChatHistory> TextCompletionAsync(ChatHistory chatHistory, LLMServiceSetting requestSetting, CancellationToken cancellationToken = default)
    {
        Verifier.NotNull(chatHistory);
        Verifier.NotNull(Client);

        ValidateMaxTokens(requestSetting.MaxTokens);
        var chatOptions = CreateCompletionsOptions(requestSetting, chatHistory);

        Response<ChatCompletions>? response = await RunAsync<Response<ChatCompletions>?>(
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

    public override async IAsyncEnumerable<ChatStreamingResult> TextCompletionStreamingAsync(ChatHistory chatHistory, LLMServiceSetting requestSetting,
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

    public override async Task<string> SpeechToTextAsync(List<string> speechFilePaths,
        string language = "en", TranscriptionFormatKind transcriptionFormat = TranscriptionFormatKind.SingleTextJson, CancellationToken cancellationToken = default)
    {
        var transcript = new StringBuilder();

        var responseFormat = transcriptionFormat switch
        {
            TranscriptionFormatKind.SubRip => AudioTranscriptionFormat.Srt,
            TranscriptionFormatKind.WebVideoTextTrack => AudioTranscriptionFormat.Vtt,
            TranscriptionFormatKind.MetadataJson => AudioTranscriptionFormat.Verbose,
            _ => AudioTranscriptionFormat.Simple
        };

        foreach (var speechFilePath in speechFilePaths)
        {
            var fileName = Path.GetFileName(speechFilePath);
            var transOptions = new AudioTranscriptionOptions
            {
                DeploymentName = DeploymentNameOrModelId,
                AudioData = await BinaryData.FromStreamAsync(File.OpenRead(speechFilePath), cancellationToken),
                ResponseFormat = responseFormat,
                Language = language,
                Filename = fileName
            };

            Response<AudioTranscription> transcriptionResponse = await Client.GetAudioTranscriptionAsync(transOptions, cancellationToken);
            AudioTranscription transcription = transcriptionResponse.Value;
            transcript.AppendLine(transcription.Text);
        }

        return transcript.ToString();
    }

    public override async Task TextToSpeechAsync(TextToSpeechRunRequest request)
    {
        var ttsRequest = new TextToSpeechRequest()
        {
            Input = LLMWorkEnv.WorkerContext.Variables.Input,
            ResponseFormat = ResponseFormats.AAC,
            Model = OpenAIConfigProvider.ProvideModel(AIServiceTypeKind.TextToSpeechQuality),
            Voice = OpenAIConfigProvider.ProvideTtsVoice(TtsVoiceKind.Nova),
            Speed = 0.9
        };

        await TtsClient.TextToSpeech.SaveSpeechToFileAsync(ttsRequest, request.SpeechFilePath);
    }

    public override Task<Stream> TextToSpeechStreamAsync(TextToSpeechRunRequest request)
    {
        var ttsRequest = new TextToSpeechRequest()
        {
            Input = LLMWorkEnv.WorkerContext.Variables.Input,
            Model = OpenAIConfigProvider.ProvideModel(AIServiceTypeKind.TextToSpeechQuality),
            Voice = OpenAIConfigProvider.ProvideTtsVoice(TtsVoiceKind.Nova)
        };

        return TtsClient.TextToSpeech.GetSpeechAsStreamAsync(ttsRequest);
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
}
