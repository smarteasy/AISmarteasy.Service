using System.Text;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Presentation;

namespace AISmarteasy.Service.Microsoft;

public class MsPowerPointConnector(string slideNumberTemplate = "# Slide {number}",
    string endOfSlideMarkerTemplate = "# End of slide {number}")
{
    public string DocToText(string filename, bool withSlideNumber = true,
        bool withEndOfSlideMarker = false, bool skipHiddenSlides = true)
    {
        using var stream = File.OpenRead(filename);
        return DocToText(stream, skipHiddenSlides: skipHiddenSlides, withEndOfSlideMarker: withEndOfSlideMarker, withSlideNumber: withSlideNumber);
    }

    public string DocToText(BinaryData data, 
        bool withSlideNumber = true, bool withEndOfSlideMarker = false, bool skipHiddenSlides = true)
    {
        using var stream = data.ToStream();
        return DocToText(stream, skipHiddenSlides: skipHiddenSlides, withEndOfSlideMarker: withEndOfSlideMarker, withSlideNumber: withSlideNumber);
    }

    public string DocToText(Stream data,
        bool withSlideNumber = true, bool withEndOfSlideMarker = false, bool skipHiddenSlides = true)
    {
        using PresentationDocument presentationDocument = PresentationDocument.Open(data, false);
        var sb = new StringBuilder();

        if (presentationDocument.PresentationPart is { Presentation: { SlideIdList: { } slideIdList } } presentationPart
            && slideIdList.Elements<SlideId>().ToList() is { Count: > 0 } slideIds)
        {
            var slideNumber = 0;
            foreach (SlideId slideId in slideIds)
            {
                slideNumber++;
                if ((string?)slideId.RelationshipId is { } relationshipId
                    && presentationPart.GetPartById(relationshipId) is SlidePart slidePart
                    && slidePart.Slide?.Descendants<DocumentFormat.OpenXml.Drawing.Text>().ToList() is { Count: > 0 } texts)
                {
                    bool isVisible = slidePart.Slide.Show ?? true;
                    if (skipHiddenSlides && !isVisible) { continue; }

                    var slideContent = new StringBuilder();
                    for (var i = 0; i < texts.Count; i++)
                    {
                        var text = texts[i];
                        slideContent.Append(text.Text);
                        if (i < texts.Count - 1)
                        {
                            slideContent.Append(' ');
                        }
                    }

                    if (slideContent.Length < 1) { continue; }
                    
                    if (withSlideNumber)
                    {
                        sb.AppendLine(slideNumberTemplate.Replace("{number}", $"{slideNumber}", StringComparison.OrdinalIgnoreCase));
                    }

                    sb.Append(slideContent);
                    sb.AppendLine();

                    if (withEndOfSlideMarker)
                    {
                        sb.AppendLine(endOfSlideMarkerTemplate.Replace("{number}", $"{slideNumber}", StringComparison.OrdinalIgnoreCase));
                    }
                }
            }
        }

        return sb.ToString().Trim();
    }
}
