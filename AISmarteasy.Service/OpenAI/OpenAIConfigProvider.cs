using AISmarteasy.Core;

namespace AISmarteasy.Service.OpenAI;

public static class OpenAIConfigProvider
{
    public static string ProvideModel(AIServiceTypeKind serviceType)
    {
        if(serviceType==AIServiceTypeKind.TextCompletion)
            return "gpt-4-1106-preview";
        return "text-embedding-ada-002";
    }
}
