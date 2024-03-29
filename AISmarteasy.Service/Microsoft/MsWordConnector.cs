﻿using System.Text;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace AISmarteasy.Service.Microsoft;

public class MsWordConnector
{
    public string DocToText(string filename)
    {
        using var stream = File.OpenRead(filename);
        return DocToText(stream);
    }

    private string DocToText(Stream data)
    {
        var wordprocessingDocument = WordprocessingDocument.Open(data, false);
        try
        {
            StringBuilder sb = new();

            MainDocumentPart? mainPart = wordprocessingDocument.MainDocumentPart;
            if (mainPart is null)
            {
                throw new InvalidOperationException("The main document part is missing.");
            }

            Body? body = mainPart.Document.Body;
            if (body is null)
            {
                throw new InvalidOperationException("The document body is missing.");
            }
            IEnumerable<Paragraph> paragraphs = body.Descendants<Paragraph>();
            foreach (Paragraph p in paragraphs)
            {
                sb.AppendLine(p.InnerText);
            }

            return sb.ToString().Trim();
        }
        finally
        {
            wordprocessingDocument.Dispose();
        }
    }
}
