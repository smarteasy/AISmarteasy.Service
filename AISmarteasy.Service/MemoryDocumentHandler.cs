using AISmarteasy.Service.Microsoft;

namespace AISmarteasy.Service;

public class MemoryDocumentHandler
{


    public MemoryDocumentHandler()
    {
    }

    public string DocToText(string filename)
    {
        var text = new MsWordConnector().DocToText(filename);
        return text;
    }
}
