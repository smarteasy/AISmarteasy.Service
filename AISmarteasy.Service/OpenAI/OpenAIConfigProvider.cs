using AISmarteasy.Core;

namespace AISmarteasy.Service.OpenAI;

public static class OpenAIConfigProvider
{
    public static string ProvideModel(AIServiceTypeKind serviceType)
    {
        switch (serviceType)
        {
            case AIServiceTypeKind.TextCompletion:
                return "gpt-4-1106-preview";
            case AIServiceTypeKind.SpeechToText:
                return "whisper-1";
            case AIServiceTypeKind.TextToSpeechSpeed:
                return "tts-1-hd";
            case AIServiceTypeKind.TextToSpeechQuality:
                return "tts-1";
            default:
                return "text-embedding-ada-002";
        }
    }

    public static string ProvideTtsVoice(TtsVoiceKind voice)
    {
        switch (voice)
        {
            case TtsVoiceKind.Echo:
                return "echo";
            case TtsVoiceKind.Fable:
                return "fable";
            case TtsVoiceKind.Onyx:
                return "onyx";
            case TtsVoiceKind.Nova:
                return "nova";
            case TtsVoiceKind.Shimmer:
                return "shimmer";
            default:
                return "alloy";
        }
    }
}