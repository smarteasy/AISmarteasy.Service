using System.Text;
using ClosedXML.Excel;

namespace AISmarteasy.Service.Microsoft;

public class MsExcelConnector(bool withWorksheetNumber = true,
    bool withEndOfWorksheetMarker = false,
    bool withQuotes = true,
    string? worksheetNumberTemplate = null,
    string? endOfWorksheetMarkerTemplate = null,
    string? rowPrefix = null,
    string? columnSeparator = null,
    string? rowSuffix = null)
{
    private const string DEFAULT_SHEET_NUMBER_TEMPLATE = "\n# Worksheet {number}\n";
    private const string DEFAULT_END_OF_SHEET_TEMPLATE = "\n# End of worksheet {number}";
    private const string DEFAULT_ROW_PREFIX = "";
    private const string DEFAULT_COLUMN_SEPARATOR = ", ";
    private const string DEFAULT_ROW_SUFFIX = "";

    private readonly string _worksheetNumberTemplate = worksheetNumberTemplate ?? DEFAULT_SHEET_NUMBER_TEMPLATE;
    private readonly string _endOfWorksheetMarkerTemplate = endOfWorksheetMarkerTemplate ?? DEFAULT_END_OF_SHEET_TEMPLATE;
    private readonly string _rowPrefix = rowPrefix ?? DEFAULT_ROW_PREFIX;
    private readonly string _columnSeparator = columnSeparator ?? DEFAULT_COLUMN_SEPARATOR;
    private readonly string _rowSuffix = rowSuffix ?? DEFAULT_ROW_SUFFIX;

    public string DocToText(string filename)
    {
        using var stream = File.OpenRead(filename);
        return DocToText(stream);
    }

    public string DocToText(BinaryData data)
    {
        using var stream = data.ToStream();
        return DocToText(stream);
    }

    public string DocToText(Stream data)
    {
        using var workbook = new XLWorkbook(data);
        var sb = new StringBuilder();

        var worksheetNumber = 0;
        foreach (var worksheet in workbook.Worksheets)
        {
            worksheetNumber++;
            if (withWorksheetNumber)
            {
                sb.AppendLine(_worksheetNumberTemplate.Replace("{number}", $"{worksheetNumber}", StringComparison.OrdinalIgnoreCase));
            }

            foreach (IXLRangeRow? row in worksheet.RangeUsed()?.RowsUsed()!)
            {
                if (row == null) { continue; }

                var cells = row.CellsUsed().ToList();

                sb.Append(_rowPrefix);
                for (var i = 0; i < cells.Count; i++)
                {
                    IXLCell? cell = cells[i];

                    if (withQuotes && cell is { Value.IsText: true })
                    {
                        sb.Append('"')
                            .Append(cell.Value.GetText().Replace("\"", "\"\"", StringComparison.Ordinal))
                            .Append('"');
                    }
                    else
                    {
                        sb.Append(cell.Value);
                    }

                    if (i < cells.Count - 1)
                    {
                        sb.Append(_columnSeparator);
                    }
                }

                sb.AppendLine(_rowSuffix);
            }

            if (withEndOfWorksheetMarker)
            {
                sb.AppendLine(_endOfWorksheetMarkerTemplate.Replace("{number}", $"{worksheetNumber}", StringComparison.OrdinalIgnoreCase));
            }
        }

        return sb.ToString().Trim();
    }
}
