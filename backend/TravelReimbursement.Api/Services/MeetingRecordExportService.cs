using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml;
using Microsoft.EntityFrameworkCore;
using TravelReimbursement.Api.Data;
using TravelReimbursement.Api.Domain;

namespace TravelReimbursement.Api.Services;

public sealed record MeetingRecordExportResult(byte[] Content, string FileName, int RecordCount);

public sealed class MeetingRecordExportService(AppDbContext db)
{
    public async Task<MeetingRecordExportResult> CreateAsync(Guid projectId, CancellationToken cancellationToken)
    {
        if (projectId == Guid.Empty)
            throw new ApiProblemException(StatusCodes.Status400BadRequest, "MEETING_RECORD_EXPORT_PROJECT_REQUIRED", "请选择需要导出的项目。");

        var project = await db.Projects.AsNoTracking()
            .Where(candidate => candidate.Id == projectId)
            .Select(candidate => new { candidate.Code, candidate.Name })
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new ApiProblemException(StatusCodes.Status404NotFound, "MEETING_RECORD_EXPORT_PROJECT_NOT_FOUND", "所选项目不存在。");

        var records = await db.MeetingRecords.AsNoTracking()
            .Where(record => record.ProjectId == projectId)
            .Include(record => record.Participants)
            .Include(record => record.Items)
            .OrderBy(record => record.MeetingDate)
            .ThenBy(record => record.CreatedAt)
            .ThenBy(record => record.Id)
            .ToListAsync(cancellationToken);

        if (records.Count == 0)
            throw new ApiProblemException(StatusCodes.Status409Conflict, "MEETING_RECORD_EXPORT_EMPTY", "所选项目暂无可导出的会议记录。");

        var sheets = records.Select(record => new MeetingRecordExportData(
            record.Id,
            record.MeetingDate,
            record.Location,
            record.CreatedAt,
            record.Participants.OrderBy(participant => participant.SortOrder)
                .Select(participant => new MeetingParticipantExportData(participant.Name, participant.Organization, participant.Title, participant.Phone))
                .ToArray(),
            record.Items.Where(item => item.Kind == MeetingRecordItemKind.Requirement).OrderBy(item => item.SortOrder)
                .Select(ToItem).ToArray(),
            record.Items.Where(item => item.Kind == MeetingRecordItemKind.WorkFocus).OrderBy(item => item.SortOrder)
                .Select(ToItem).ToArray()))
            .ToArray();

        var fileName = $"{SafeFileName(project.Code)}_{SafeFileName(project.Name)}_会议记录.xlsx";
        return new MeetingRecordExportResult(MeetingRecordWorkbookWriter.Write(sheets), fileName, records.Count);
    }

    private static MeetingRecordItemExportData ToItem(MeetingRecordItem item) =>
        new(item.Content, item.Status, item.DueDate, item.Owner);

    private static string SafeFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var result = new string(value.Trim().Select(character => character < ' ' || invalid.Contains(character) ? '_' : character).ToArray())
            .Trim('.', ' ');
        return string.IsNullOrWhiteSpace(result) ? "项目" : result;
    }
}

internal sealed record MeetingRecordExportData(
    Guid Id,
    DateOnly MeetingDate,
    string Location,
    DateTimeOffset CreatedAt,
    IReadOnlyList<MeetingParticipantExportData> Participants,
    IReadOnlyList<MeetingRecordItemExportData> Requirements,
    IReadOnlyList<MeetingRecordItemExportData> WorkFocuses);

internal sealed record MeetingParticipantExportData(string Name, string? Organization, string? Title, string? Phone);
internal sealed record MeetingRecordItemExportData(string Content, string? Status, DateOnly? DueDate, string? Owner);

internal static class MeetingRecordWorkbookWriter
{
    private const string SpreadsheetNamespace = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private const string RelationshipNamespace = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";

    public static byte[] Write(IReadOnlyList<MeetingRecordExportData> records)
    {
        if (records.Count == 0) throw new ArgumentException("至少需要一条会议记录。", nameof(records));

        var ordered = records.OrderBy(record => record.MeetingDate).ThenBy(record => record.CreatedAt).ThenBy(record => record.Id).ToArray();
        var sheets = CreateSheets(ordered);
        using var output = new MemoryStream();
        using (var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteContentTypes(archive, sheets.Count);
            WriteRootRelationships(archive);
            WriteWorkbook(archive, sheets);
            WriteWorkbookRelationships(archive, sheets.Count);
            WriteStyles(archive);
            for (var index = 0; index < sheets.Count; index++) WriteWorksheet(archive, index + 1, sheets[index]);
        }
        return output.ToArray();
    }

    private static IReadOnlyList<MeetingRecordSheet> CreateSheets(IReadOnlyList<MeetingRecordExportData> records)
    {
        var dateCounters = new Dictionary<DateOnly, int>();
        return records.Select(record =>
        {
            dateCounters.TryGetValue(record.MeetingDate, out var count);
            count++;
            dateCounters[record.MeetingDate] = count;
            return CreateSheet($"{record.MeetingDate:yyyyMMdd}-{count:00}", record);
        }).ToArray();
    }

    private static MeetingRecordSheet CreateSheet(string name, MeetingRecordExportData record)
    {
        var rows = new List<MeetingRecordSheetRow>();
        var merges = new List<string>();

        AddMergedRow(rows, merges, "会议记录", 1, 1, 9, 30);

        var dateLocation = NewCells(3);
        Set(dateLocation, 1, "日期", 2);
        Set(dateLocation, 2, record.MeetingDate.ToString("yyyy年MM月dd日", CultureInfo.InvariantCulture), 3);
        Set(dateLocation, 4, "地点", 2);
        Set(dateLocation, 5, record.Location, 3);
        rows.Add(new MeetingRecordSheetRow(24, dateLocation));
        merges.Add($"B{rows.Count}:C{rows.Count}");
        merges.Add($"E{rows.Count}:I{rows.Count}");

        AddMergedRow(rows, merges, "参会人员", 2, 1, 9, 23);
        rows.Add(HeaderRow("姓名", "单位", "职务", "联系电话"));
        AddFourColumnMerges(merges, rows.Count);
        foreach (var participant in Pad(record.Participants, 3))
        {
            var cells = NewCells(3);
            if (participant is not null)
            {
                Set(cells, 1, participant.Name, 3);
                Set(cells, 3, participant.Organization, 3);
                Set(cells, 5, participant.Title, 3);
                Set(cells, 8, participant.Phone, 3);
            }
            rows.Add(new MeetingRecordSheetRow(EstimateHeight(participant is null ? null : string.Join(' ', participant.Name, participant.Organization, participant.Title, participant.Phone), 30, 23), cells));
            AddFourColumnMerges(merges, rows.Count);
        }

        AddMergedRow(rows, merges, "会议内容摘要", 2, 1, 9, 23);
        AddItems(rows, merges, "需求内容", record.Requirements, 1);
        AddItems(rows, merges, "工作重点", record.WorkFocuses, 3);
        return new MeetingRecordSheet(name, rows, merges);
    }

    private static void AddItems(
        ICollection<MeetingRecordSheetRow> rows,
        ICollection<string> merges,
        string category,
        IReadOnlyList<MeetingRecordItemExportData> items,
        int minimumRows)
    {
        var header = NewCells(2);
        Set(header, 1, category, 2);
        Set(header, 6, "状态", 2);
        Set(header, 7, "完成时间", 2);
        Set(header, 9, "负责人", 2);
        rows.Add(new MeetingRecordSheetRow(23, header));
        AddItemMerges(merges, rows.Count);

        foreach (var item in Pad(items, minimumRows))
        {
            var cells = NewCells(3);
            if (item is not null)
            {
                Set(cells, 1, item.Content, 4);
                Set(cells, 6, item.Status, 3);
                Set(cells, 7, item.DueDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), 3);
                Set(cells, 9, item.Owner, 3);
            }
            rows.Add(new MeetingRecordSheetRow(EstimateHeight(item?.Content, 42, 28), cells));
            AddItemMerges(merges, rows.Count);
        }
    }

    private static MeetingRecordSheetRow HeaderRow(string first, string second, string third, string fourth)
    {
        var cells = NewCells(2);
        Set(cells, 1, first, 2);
        Set(cells, 3, second, 2);
        Set(cells, 5, third, 2);
        Set(cells, 8, fourth, 2);
        return new MeetingRecordSheetRow(23, cells);
    }

    private static void AddMergedRow(ICollection<MeetingRecordSheetRow> rows, ICollection<string> merges, string value, int style, int fromColumn, int toColumn, double height)
    {
        var cells = NewCells(style);
        Set(cells, fromColumn, value, style);
        rows.Add(new MeetingRecordSheetRow(height, cells));
        merges.Add($"{ColumnName(fromColumn)}{rows.Count}:{ColumnName(toColumn)}{rows.Count}");
    }

    private static MeetingRecordCell[] NewCells(int style) =>
        Enumerable.Range(0, 9).Select(_ => new MeetingRecordCell(string.Empty, style)).ToArray();

    private static void Set(MeetingRecordCell[] cells, int column, string? value, int style) =>
        cells[column - 1] = new MeetingRecordCell(value ?? string.Empty, style);

    private static IEnumerable<T?> Pad<T>(IReadOnlyList<T> values, int minimum) where T : class
    {
        foreach (var value in values) yield return value;
        for (var index = values.Count; index < minimum; index++) yield return null;
    }

    private static double EstimateHeight(string? value, int charactersPerLine, double minimum)
    {
        if (string.IsNullOrWhiteSpace(value)) return minimum;
        var lines = value.Replace("\r", string.Empty, StringComparison.Ordinal).Split('\n')
            .Sum(line => Math.Max(1, (int)Math.Ceiling(line.Length / (double)charactersPerLine)));
        return Math.Min(120, Math.Max(minimum, lines * 17 + 8));
    }

    private static void AddFourColumnMerges(ICollection<string> merges, int row)
    {
        merges.Add($"A{row}:B{row}");
        merges.Add($"C{row}:D{row}");
        merges.Add($"E{row}:G{row}");
        merges.Add($"H{row}:I{row}");
    }

    private static void AddItemMerges(ICollection<string> merges, int row)
    {
        merges.Add($"A{row}:E{row}");
        merges.Add($"G{row}:H{row}");
    }

    private static void WriteContentTypes(ZipArchive archive, int sheetCount) => WriteXml(archive, "[Content_Types].xml", writer =>
    {
        writer.WriteStartElement("Types", "http://schemas.openxmlformats.org/package/2006/content-types");
        WriteContentType(writer, "Default", "Extension", "rels", "application/vnd.openxmlformats-package.relationships+xml");
        WriteContentType(writer, "Default", "Extension", "xml", "application/xml");
        WriteContentType(writer, "Override", "PartName", "/xl/workbook.xml", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml");
        WriteContentType(writer, "Override", "PartName", "/xl/styles.xml", "application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml");
        for (var index = 1; index <= sheetCount; index++)
            WriteContentType(writer, "Override", "PartName", $"/xl/worksheets/sheet{index}.xml", "application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml");
        writer.WriteEndElement();
    });

    private static void WriteContentType(XmlWriter writer, string element, string key, string value, string contentType)
    {
        writer.WriteStartElement(element);
        writer.WriteAttributeString(key, value);
        writer.WriteAttributeString("ContentType", contentType);
        writer.WriteEndElement();
    }

    private static void WriteRootRelationships(ZipArchive archive) => WriteXml(archive, "_rels/.rels", writer =>
    {
        writer.WriteStartElement("Relationships", "http://schemas.openxmlformats.org/package/2006/relationships");
        WriteRelationship(writer, "rId1", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument", "xl/workbook.xml");
        writer.WriteEndElement();
    });

    private static void WriteWorkbook(ZipArchive archive, IReadOnlyList<MeetingRecordSheet> sheets) => WriteXml(archive, "xl/workbook.xml", writer =>
    {
        writer.WriteStartElement("workbook", SpreadsheetNamespace);
        writer.WriteAttributeString("xmlns", "r", null, RelationshipNamespace);
        writer.WriteStartElement("sheets");
        for (var index = 0; index < sheets.Count; index++)
        {
            writer.WriteStartElement("sheet");
            writer.WriteAttributeString("name", sheets[index].Name);
            writer.WriteAttributeString("sheetId", (index + 1).ToString(CultureInfo.InvariantCulture));
            writer.WriteAttributeString("r", "id", null, $"rId{index + 1}");
            writer.WriteEndElement();
        }
        writer.WriteEndElement();
        writer.WriteStartElement("definedNames");
        for (var index = 0; index < sheets.Count; index++)
        {
            writer.WriteStartElement("definedName");
            writer.WriteAttributeString("name", "_xlnm.Print_Area");
            writer.WriteAttributeString("localSheetId", index.ToString(CultureInfo.InvariantCulture));
            writer.WriteString($"'{sheets[index].Name.Replace("'", "''", StringComparison.Ordinal)}'!$A$1:$I${sheets[index].Rows.Count}");
            writer.WriteEndElement();
        }
        writer.WriteEndElement();
        writer.WriteEndElement();
    });

    private static void WriteWorkbookRelationships(ZipArchive archive, int sheetCount) => WriteXml(archive, "xl/_rels/workbook.xml.rels", writer =>
    {
        writer.WriteStartElement("Relationships", "http://schemas.openxmlformats.org/package/2006/relationships");
        for (var index = 1; index <= sheetCount; index++)
            WriteRelationship(writer, $"rId{index}", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet", $"worksheets/sheet{index}.xml");
        WriteRelationship(writer, $"rId{sheetCount + 1}", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles", "styles.xml");
        writer.WriteEndElement();
    });

    private static void WriteStyles(ZipArchive archive) => WriteXml(archive, "xl/styles.xml", writer =>
    {
        writer.WriteStartElement("styleSheet", SpreadsheetNamespace);
        writer.WriteStartElement("fonts"); writer.WriteAttributeString("count", "3");
        WriteFont(writer, "等线", 10.5, false);
        WriteFont(writer, "黑体", 14, true);
        WriteFont(writer, "黑体", 10.5, true);
        writer.WriteEndElement();
        writer.WriteStartElement("fills"); writer.WriteAttributeString("count", "2");
        writer.WriteStartElement("fill"); writer.WriteStartElement("patternFill"); writer.WriteAttributeString("patternType", "none"); writer.WriteEndElement(); writer.WriteEndElement();
        writer.WriteStartElement("fill"); writer.WriteStartElement("patternFill"); writer.WriteAttributeString("patternType", "gray125"); writer.WriteEndElement(); writer.WriteEndElement();
        writer.WriteEndElement();
        writer.WriteStartElement("borders"); writer.WriteAttributeString("count", "2");
        WriteBorder(writer, false);
        WriteBorder(writer, true);
        writer.WriteEndElement();
        writer.WriteStartElement("cellStyleXfs"); writer.WriteAttributeString("count", "1");
        WriteXf(writer, 0, 0, false, "center");
        writer.WriteEndElement();
        writer.WriteStartElement("cellXfs"); writer.WriteAttributeString("count", "5");
        WriteXf(writer, 0, 0, false, "left");
        WriteXf(writer, 1, 1, true, "center");
        WriteXf(writer, 2, 1, true, "center");
        WriteXf(writer, 0, 1, true, "center");
        WriteXf(writer, 0, 1, true, "left");
        writer.WriteEndElement();
        writer.WriteStartElement("cellStyles"); writer.WriteAttributeString("count", "1");
        writer.WriteStartElement("cellStyle"); writer.WriteAttributeString("name", "Normal"); writer.WriteAttributeString("xfId", "0"); writer.WriteAttributeString("builtinId", "0"); writer.WriteEndElement();
        writer.WriteEndElement();
        writer.WriteEndElement();
    });

    private static void WriteFont(XmlWriter writer, string name, double size, bool bold)
    {
        writer.WriteStartElement("font");
        if (bold) writer.WriteElementString("b", string.Empty);
        writer.WriteStartElement("sz"); writer.WriteAttributeString("val", size.ToString("0.#", CultureInfo.InvariantCulture)); writer.WriteEndElement();
        writer.WriteStartElement("name"); writer.WriteAttributeString("val", name); writer.WriteEndElement();
        writer.WriteStartElement("family"); writer.WriteAttributeString("val", "3"); writer.WriteEndElement();
        writer.WriteEndElement();
    }

    private static void WriteBorder(XmlWriter writer, bool medium)
    {
        writer.WriteStartElement("border");
        foreach (var side in new[] { "left", "right", "top", "bottom" })
        {
            writer.WriteStartElement(side);
            if (medium)
            {
                writer.WriteAttributeString("style", "medium");
                writer.WriteStartElement("color"); writer.WriteAttributeString("auto", "1"); writer.WriteEndElement();
            }
            writer.WriteEndElement();
        }
        writer.WriteElementString("diagonal", string.Empty);
        writer.WriteEndElement();
    }

    private static void WriteXf(XmlWriter writer, int fontId, int borderId, bool applyAlignment, string horizontal)
    {
        writer.WriteStartElement("xf");
        writer.WriteAttributeString("numFmtId", "0");
        writer.WriteAttributeString("fontId", fontId.ToString(CultureInfo.InvariantCulture));
        writer.WriteAttributeString("fillId", "0");
        writer.WriteAttributeString("borderId", borderId.ToString(CultureInfo.InvariantCulture));
        writer.WriteAttributeString("xfId", "0");
        if (borderId > 0) writer.WriteAttributeString("applyBorder", "1");
        if (applyAlignment)
        {
            writer.WriteAttributeString("applyAlignment", "1");
            writer.WriteStartElement("alignment");
            writer.WriteAttributeString("horizontal", horizontal);
            writer.WriteAttributeString("vertical", "center");
            writer.WriteAttributeString("wrapText", "1");
            writer.WriteEndElement();
        }
        writer.WriteEndElement();
    }

    private static void WriteWorksheet(ZipArchive archive, int index, MeetingRecordSheet sheet) => WriteXml(archive, $"xl/worksheets/sheet{index}.xml", writer =>
    {
        writer.WriteStartElement("worksheet", SpreadsheetNamespace);
        writer.WriteStartElement("sheetPr"); writer.WriteStartElement("pageSetUpPr"); writer.WriteAttributeString("fitToPage", "1"); writer.WriteEndElement(); writer.WriteEndElement();
        writer.WriteStartElement("dimension"); writer.WriteAttributeString("ref", $"A1:I{sheet.Rows.Count}"); writer.WriteEndElement();
        writer.WriteStartElement("sheetViews"); writer.WriteStartElement("sheetView"); writer.WriteAttributeString("workbookViewId", "0"); writer.WriteEndElement(); writer.WriteEndElement();
        writer.WriteStartElement("sheetFormatPr"); writer.WriteAttributeString("defaultRowHeight", "15"); writer.WriteEndElement();
        writer.WriteStartElement("cols");
        var widths = new[] { 9d, 9d, 13d, 13d, 10d, 10d, 11d, 11d, 13d };
        for (var column = 1; column <= widths.Length; column++)
        {
            writer.WriteStartElement("col"); writer.WriteAttributeString("min", column.ToString(CultureInfo.InvariantCulture)); writer.WriteAttributeString("max", column.ToString(CultureInfo.InvariantCulture)); writer.WriteAttributeString("width", widths[column - 1].ToString(CultureInfo.InvariantCulture)); writer.WriteAttributeString("customWidth", "1"); writer.WriteEndElement();
        }
        writer.WriteEndElement();
        writer.WriteStartElement("sheetData");
        for (var rowIndex = 0; rowIndex < sheet.Rows.Count; rowIndex++)
        {
            var row = sheet.Rows[rowIndex];
            writer.WriteStartElement("row");
            writer.WriteAttributeString("r", (rowIndex + 1).ToString(CultureInfo.InvariantCulture));
            writer.WriteAttributeString("ht", row.Height.ToString("0.#", CultureInfo.InvariantCulture));
            writer.WriteAttributeString("customHeight", "1");
            for (var columnIndex = 0; columnIndex < row.Cells.Count; columnIndex++)
                WriteCell(writer, $"{ColumnName(columnIndex + 1)}{rowIndex + 1}", row.Cells[columnIndex]);
            writer.WriteEndElement();
        }
        writer.WriteEndElement();
        writer.WriteStartElement("mergeCells"); writer.WriteAttributeString("count", sheet.Merges.Count.ToString(CultureInfo.InvariantCulture));
        foreach (var merge in sheet.Merges) { writer.WriteStartElement("mergeCell"); writer.WriteAttributeString("ref", merge); writer.WriteEndElement(); }
        writer.WriteEndElement();
        writer.WriteStartElement("printOptions"); writer.WriteAttributeString("horizontalCentered", "1"); writer.WriteEndElement();
        writer.WriteStartElement("pageMargins"); writer.WriteAttributeString("left", "0.7"); writer.WriteAttributeString("right", "0.7"); writer.WriteAttributeString("top", "0.75"); writer.WriteAttributeString("bottom", "0.75"); writer.WriteAttributeString("header", "0.3"); writer.WriteAttributeString("footer", "0.3"); writer.WriteEndElement();
        writer.WriteStartElement("pageSetup"); writer.WriteAttributeString("paperSize", "9"); writer.WriteAttributeString("orientation", "portrait"); writer.WriteAttributeString("fitToWidth", "1"); writer.WriteAttributeString("fitToHeight", "0"); writer.WriteEndElement();
        writer.WriteEndElement();
    });

    private static void WriteCell(XmlWriter writer, string reference, MeetingRecordCell cell)
    {
        writer.WriteStartElement("c");
        writer.WriteAttributeString("r", reference);
        writer.WriteAttributeString("s", cell.Style.ToString(CultureInfo.InvariantCulture));
        if (cell.Value.Length > 0)
        {
            writer.WriteAttributeString("t", "inlineStr");
            writer.WriteStartElement("is");
            writer.WriteStartElement("t");
            if (char.IsWhiteSpace(cell.Value[0]) || char.IsWhiteSpace(cell.Value[^1])) writer.WriteAttributeString("xml", "space", null, "preserve");
            writer.WriteString(cell.Value);
            writer.WriteEndElement();
            writer.WriteEndElement();
        }
        writer.WriteEndElement();
    }

    private static string ColumnName(int column)
    {
        var name = string.Empty;
        while (column > 0) { column--; name = (char)('A' + column % 26) + name; column /= 26; }
        return name;
    }

    private static void WriteRelationship(XmlWriter writer, string id, string type, string target)
    {
        writer.WriteStartElement("Relationship"); writer.WriteAttributeString("Id", id); writer.WriteAttributeString("Type", type); writer.WriteAttributeString("Target", target); writer.WriteEndElement();
    }

    private static void WriteXml(ZipArchive archive, string path, Action<XmlWriter> write)
    {
        var entry = archive.CreateEntry(path, CompressionLevel.Fastest);
        using var stream = entry.Open();
        using var writer = XmlWriter.Create(stream, new XmlWriterSettings { Encoding = new UTF8Encoding(false), Indent = false, CloseOutput = false });
        write(writer);
    }

    private sealed record MeetingRecordSheet(string Name, IReadOnlyList<MeetingRecordSheetRow> Rows, IReadOnlyList<string> Merges);
    private sealed record MeetingRecordSheetRow(double Height, IReadOnlyList<MeetingRecordCell> Cells);
    private sealed record MeetingRecordCell(string Value, int Style);
}
