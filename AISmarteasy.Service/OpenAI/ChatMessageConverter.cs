using Azure.AI.OpenAI;

namespace AISmarteasy.Service.OpenAI;

public static class ChatMessageConverter
{
    public static ChatRequestMessage Convert(ChatRole from, string message)
    {
        if (from == ChatRole.User)
        {
            return new ChatRequestUserMessage(message);
        }

        if (from == ChatRole.Assistant)
        {
            return new ChatRequestAssistantMessage(message);
        }

        return new ChatRequestSystemMessage(message);
    }
}
