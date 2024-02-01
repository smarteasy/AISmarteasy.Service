using System.Text;
using AISmarteasy.Core;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace AISmarteasy.Service;

public class PdfConnector
{
    public List<DocumentPage> DocToText(string filename)
    {
        using var stream = File.OpenRead(filename);
        return DocToText(stream);
    }

    public List<DocumentPage> DocToText(BinaryData data)
    {
        using var stream = data.ToStream();
        return DocToText(stream);
    }

    public List<DocumentPage> DocToText(Stream data)
    {
        var result = new List<DocumentPage>();
        StringBuilder sb = new();
        using var pdfDocument = PdfDocument.Open(data);
        foreach (Page? page in pdfDocument.GetPages())
        {
            string? text = ContentOrderTextExtractor.GetText(page);
            result.Add(new DocumentPage(text, page.Number));
        }

        return result;
    }
}
