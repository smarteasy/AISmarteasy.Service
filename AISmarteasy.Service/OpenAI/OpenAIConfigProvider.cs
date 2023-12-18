namespace AISmarteasy.Service.OpenAI;

public static class OpenAIConfigProvider
{
    public static string ProvideCompletionModel()
    {
        return "gpt-3.5-turbo-instruct";
    }
    public static string ProvideChatCompletionModel()
    {
        return "gpt-4-1106-preview";
    }

    public static string ProvideEmbeddingModel()
    {
        return "text-embedding-ada-002";
    }

}
