using System.Text.Json;
using AISmarteasy.Core;

namespace AISmarteasy.Service.OpenAI;

public static class Gpt3Settings
{
    public static Dictionary<string, int> Encoder => BuildEncoderFunction.Value;

    public static Dictionary<Tuple<string, string>, int> BpeRanks => BuildBpeRanksFunction.Value;

    private static readonly Lazy<Dictionary<string, int>> BuildEncoderFunction = new(BuildEncoder);

    private static readonly Lazy<Dictionary<Tuple<string, string>, int>> BuildBpeRanksFunction = new(BuildBpeRanks);

    private static Dictionary<Tuple<string, string>, int> BuildBpeRanks()
    {
        string[] lines = EmbeddedTokenizersGpt3Resource.ReadBytePairEncodingTable().Split('\n');
        List<Tuple<string, string>> bpeMerges = new ArraySegment<string>(lines, 1, lines.Length - 1)
            .Where(x => x.Trim().Length > 0)
            .Select(x =>
            {
                string[] y = x.Split(' ');
                return new Tuple<string, string>(y[0], y[1]);
            }).ToList();
        return DictZip(bpeMerges, Range(0, bpeMerges.Count));
    }

    private static Dictionary<string, int> BuildEncoder()
    {
        string json = EmbeddedTokenizersGpt3Resource.ReadEncodingTable();
        var encoder = JsonSerializer.Deserialize<Dictionary<string, int>>(json);
        return encoder ?? throw new CoreException("Encoding table deserialization returned NULL");
    }

    private static Dictionary<Tuple<string, string>, int> DictZip(List<Tuple<string, string>> x, List<int> y)
    {
        var result = new Dictionary<Tuple<string, string>, int>();
        for (int i = 0; i < x.Count; i++)
        {
            result.Add(x[i], y[i]);
        }

        return result;
    }

    private static List<int> Range(int x, int y)
    {
        return Enumerable.Range(x, y - x).ToList();
    }
}
