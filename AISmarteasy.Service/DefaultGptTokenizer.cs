using AISmarteasy.Core;
using AISmarteasy.Service.OpenAI;

namespace AISmarteasy.Service;

public class DefaultGptTokenizer : ITextTokenizer
{
    public int CountTokens(string text)
    {
        return Gpt3Tokenizer.Encode(text).Count;
    }

    public static int StaticCountTokens(string text)
    {
        return Gpt3Tokenizer.Encode(text).Count;
    }
}
