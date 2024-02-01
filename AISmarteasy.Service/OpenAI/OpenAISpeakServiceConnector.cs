using AISmarteasy.Core;
using Azure.AI.OpenAI;
using Azure.Core;
using Azure.Core.Pipeline;

namespace AISmarteasy.Service.OpenAI;

public class OpenAISpeakServiceConnector 
{
    protected OpenAIClient Client { get; }

    private protected string DeploymentNameOrModelId { get; set; }

    public OpenAISpeakServiceConnector(AIServiceTypeKind serviceType, string apiKey, string? organization = null,
        HttpClient? httpClient = null)
    {
        DeploymentNameOrModelId = OpenAIConfigProvider.ProvideModel(serviceType);
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

    //public override async Task<string> RunSpeechToTextAsync(SpeechToTextRunRequest request, CancellationToken cancellationToken = default)
    //{
    //    var transcript = new StringBuilder();

    //    var responseFormat = request.TranscriptionFormat switch
    //    {
    //        TranscriptionFormatKind.SubRip => AudioTranscriptionFormat.Srt,
    //        TranscriptionFormatKind.WebVideoTextTrack => AudioTranscriptionFormat.Vtt,
    //        TranscriptionFormatKind.MetadataJson => AudioTranscriptionFormat.Verbose,
    //        _ => AudioTranscriptionFormat.Simple
    //    };

    //    if (request.SpeechSourceType == SpeechSourceTypeKind.Files)
    //    {
    //        Verifier.NotNull(request.SpeechFilePaths);

    //        foreach (var speechFilePath in request.SpeechFilePaths)
    //        {
    //            var fileName = Path.GetFileName(speechFilePath);
    //            var transOptions = new AudioTranscriptionOptions
    //            {
    //                DeploymentName = DeploymentNameOrModelId,
    //                AudioData = await BinaryData.FromStreamAsync(File.OpenRead(speechFilePath), cancellationToken),
    //                ResponseFormat = responseFormat,
    //                Language = request.Language,
    //                Filename = fileName
    //            };

    //            Response<AudioTranscription> transcriptionResponse = await Client.GetAudioTranscriptionAsync(transOptions, cancellationToken);
    //            AudioTranscription transcription = transcriptionResponse.Value;
    //            transcript.AppendLine(transcription.Text);
    //        }
    //    }
    //    else
    //    {
    //        var transOptions = new AudioTranscriptionOptions
    //        {
    //            DeploymentName = DeploymentNameOrModelId,
    //            AudioData = new BinaryData(request.SpeechData),
    //            ResponseFormat = responseFormat,
    //            Language = request.Language,
    //            Filename = "./temp.mp3"
    //        };

    //        Response<AudioTranscription> transcriptionResponse = await Client.GetAudioTranscriptionAsync(transOptions, cancellationToken);
    //        AudioTranscription transcription = transcriptionResponse.Value;
    //        transcript.AppendLine(transcription.Text);
    //    }
    //    return transcript.ToString();
    //}

    //public override async Task GenerateAudioAsync(AudioGenerationRequest request)
    //{
    //    var ttsRequest = new TextToSpeechRequest()
    //    {
    //        Input = LLMWorkEnv.WorkerContext.Variables.Input,
    //        ResponseFormat = ResponseFormats.AAC,
    //        Model = OpenAIConfigProvider.ProvideModel(AIServiceTypeKind.TextToSpeechQuality),
    //        Voice = request.Voice,
    //        Speed = 0.9
    //    };

    //    await TtsClient.TextToSpeech.SaveSpeechToFileAsync(ttsRequest, request.SpeechFilePath);
    //}

    //public override Task<Stream> GenerateAudioStreamAsync(AudioGenerationRequest request)
    //{
    //    var ttsRequest = new TextToSpeechRequest()
    //    {
    //        Input = LLMWorkEnv.WorkerContext.Variables.Input,
    //        Model = OpenAIConfigProvider.ProvideModel(AIServiceTypeKind.TextToSpeechQuality),
    //        Voice = request.Voice
    //    };

    //    return TtsClient.TextToSpeech.GetSpeechAsStreamAsync(ttsRequest);
    //}
}
