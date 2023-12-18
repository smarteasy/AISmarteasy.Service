using AISmarteasy.Core;
using Azure.AI.OpenAI;

namespace AISmarteasy.Service.OpenAI;

internal sealed class ChatResult 
{
    public ChatResult(ChatCompletions resultData, ChatChoice choice)
    {
        Verifier.NotNull(choice);

        ModelResult = new(new ChatModelResult(resultData, choice));
    }

    public ModelResult ModelResult { get; }
}
