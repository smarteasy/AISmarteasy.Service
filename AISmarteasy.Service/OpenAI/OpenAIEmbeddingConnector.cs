using AISmarteasy.Core;
using Azure.AI.OpenAI;
using Azure.Core;
using Azure.Core.Pipeline;

namespace AISmarteasy.Service.OpenAI;

public class OpenAIEmbeddingConnector : ITextEmbeddingConnector
{
    protected OpenAIClient Client { get; }

    private protected string DeploymentNameOrModelId { get; set; }

    public OpenAIEmbeddingConnector(AIServiceTypeKind serviceType, string apiKey, string? organization = null,
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

    public async Task<ReadOnlyMemory<float>> GenerateEmbeddingsAsync(EmbeddingRequest request, CancellationToken cancellationToken = default)
    {
        Verifier.NotNull(Client);

        var inputs = new List<string> { request.Data.Text };

        var options = new EmbeddingsOptions(OpenAIConfigProvider.ProvideModel(AIServiceTypeKind.Embedding), inputs);

        var response = await Client.GetEmbeddingsAsync(options, cancellationToken);

        if (response is null)
        {
            throw new CoreException("Text embedding null response");
        }

        if (response.Value.Data.Count == 0)
        {
            throw new CoreException("Text embedding not found");
        }

        return response.Value.Data[0].Embedding;
    }
}
