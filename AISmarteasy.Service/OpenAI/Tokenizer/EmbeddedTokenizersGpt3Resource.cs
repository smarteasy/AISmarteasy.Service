using System.Reflection;
using AISmarteasy.Core;

namespace AISmarteasy.Service.OpenAI;


public static class EmbeddedTokenizersGpt3Resource
{
    private static readonly string? Namespace = typeof(EmbeddedTokenizersGpt3Resource).Namespace;

    internal static string ReadBytePairEncodingTable()
    {
        return Read("vocab.bpe");
    }

    internal static string ReadEncodingTable()
    {
        return Read("encoder.json");
    }

    private static string Read(string fileName)
    {
        Assembly? assembly = typeof(EmbeddedTokenizersGpt3Resource).GetTypeInfo().Assembly;
        if (assembly == null) { throw new CoreException($"[{Namespace}] {fileName} assembly not found"); }

        var resourceName = $"{Namespace}." + fileName;
        using Stream? resource = assembly.GetManifestResourceStream(resourceName);
        if (resource == null) { throw new CoreException($"{resourceName} resource not found"); }

        using var reader = new StreamReader(resource);
        return reader.ReadToEnd();
    }
}
