namespace AISmarteasy.Service.OpenAI;

public static class OpenAIConfigProvider
{
    public static string ProvideChatCompletionModel()
    {
        return "gpt-4-1106-preview";
    }

    public static string ProvideEmbeddingModel()
    {
        return "text-embedding-ada-002";
    }

}
