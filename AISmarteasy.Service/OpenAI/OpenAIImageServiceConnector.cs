using AISmarteasy.Core;
using Azure.AI.OpenAI;
using Azure.Core;
using Azure.Core.Pipeline;

namespace AISmarteasy.Service.OpenAI;

public class OpenAIImageServiceConnector
{
    protected OpenAIClient Client { get; }

    private protected string DeploymentNameOrModelId { get; set; }

    public OpenAIImageServiceConnector(AIServiceTypeKind serviceType, string apiKey, string? organization = null,
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

    //public override async Task<string> GenerateImageAsync(ImageGenerationRequest request, CancellationToken cancellationToken = default)
    //{
    //    var size = (request.Width, request.Height) switch
    //    {
    //        (1024, 1024) => ImageSize.Size1024x1024,
    //        (1792, 1024) => ImageSize.Size1792x1024,
    //        (1024, 1792) => ImageSize.Size1024x1792,
    //        _ => throw new NotSupportedException("Dall-E 3 can only generate images of the following sizes 1024x1024, 1792x1024, or 1024x1792")
    //    };

    //    Response<ImageGenerations> imageGenerations;
    //    try
    //    {
    //        imageGenerations = await Client.GetImageGenerationsAsync(
    //            new ImageGenerationOptions
    //            {
    //                DeploymentName = DeploymentNameOrModelId,
    //                Prompt = request.ImageDescription,
    //                Size = size
    //            }, cancellationToken).ConfigureAwait(false);
    //    }
    //    catch (RequestFailedException e)
    //    {
    //        throw e.ToHttpOperationException();
    //    }

    //    if (!imageGenerations.HasValue)
    //    {
    //        throw new CoreException("The response does not contain an image result");
    //    }

    //    if (imageGenerations.Value.Data.Count == 0)
    //    {
    //        throw new CoreException("The response does not contain any image");
    //    }

    //    return imageGenerations.Value.Data[0].Url.AbsoluteUri;
    //}
}
