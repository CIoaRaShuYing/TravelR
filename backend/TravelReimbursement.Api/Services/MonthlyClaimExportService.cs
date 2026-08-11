using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml;
using Microsoft.EntityFrameworkCore;
using TravelReimbursement.Api.Data;
using TravelReimbursement.Api.Domain;

namespace TravelReimbursement.Api.Services;

public sealed record MonthlyClaimExportResult(byte[] Content, string FileName, DateOnly From, DateOnly To, int ClaimCount);
public sealed record MonthlyClaimArchiveResult(Stream Content, string FileName, DateOnly From, DateOnly To, int ClaimCount, int AttachmentCount);

public sealed class MonthlyClaimExportService(AppDbContext db, IPrivateFileStore fileStore)
{
    public async Task<MonthlyClaimExportResult> CreateAsync(Guid projectId, DateOnly? submittedFrom, DateOnly? submittedTo, CancellationToken cancellationToken)
    {
        var package = await CreatePackageAsync(projectId, submittedFrom, submittedTo, cancellationToken);
        return new MonthlyClaimExportResult(package.Workbook, package.WorkbookFileName, package.From, package.To, package.ClaimCount);
    }

    public async Task<MonthlyClaimArchiveResult> CreateArchiveAsync(Guid projectId, DateOnly? submittedFrom, DateOnly? submittedTo, CancellationToken cancellationToken)
    {
        var package = await CreatePackageAsync(projectId, submittedFrom, submittedTo, cancellationToken);
        var archiveFileName = Path.ChangeExtension(package.WorkbookFileName, ".zip");
        var tempPath = Path.Combine(Path.GetTempPath(), $"travel-reimbursement-export-{Guid.NewGuid():N}.zip");
        try
        {
            await using (var output = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, FileOptions.Asynchronous))
            {
                await MonthlyClaimArchiveWriter.WriteAsync(output, package.WorkbookFileName, package.Workbook, package.Attachments, fileStore, cancellationToken);
            }
            var stream = new FileStream(tempPath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, FileOptions.Asynchronous | FileOptions.DeleteOnClose);
            return new MonthlyClaimArchiveResult(stream, archiveFileName, package.From, package.To, package.ClaimCount, package.Attachments.Count);
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException or UnauthorizedAccessException or IOException)
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
            throw new ApiProblemException(StatusCodes.Status409Conflict, "EXPORT_ATTACHMENT_UNAVAILABLE", "报销凭证文件缺失或无法读取，导出已取消。");
        }
        catch
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
            throw;
        }
    }

    private async Task<MonthlyClaimExportPackage> CreatePackageAsync(Guid projectId, DateOnly? submittedFrom, DateOnly? submittedTo, CancellationToken cancellationToken)
    {
        var project = await db.Projects.AsNoTracking().SingleOrDefaultAsync(x => x.Id == projectId, cancellationToken)
            ?? throw new ApiProblemException(StatusCodes.Status404NotFound, "PROJECT_NOT_FOUND", "项目不存在。");
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, ChinaTimeZone()).DateTime);
        var from = submittedFrom ?? PreviousMonthDay(today, 10);
        var to = submittedTo ?? new DateOnly(today.Year, today.Month, Math.Min(10, DateTime.DaysInMonth(today.Year, today.Month)));
        if (to < from) throw new ApiProblemException(StatusCodes.Status400BadRequest, "EXPORT_DATE_INVALID", "导出结束日期不能早于开始日期。");

        var fromInstant = new DateTimeOffset(from.ToDateTime(TimeOnly.MinValue), TimeSpan.FromHours(8)).ToUniversalTime();
        var toExclusive = new DateTimeOffset(to.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.FromHours(8)).ToUniversalTime();
        var claims = await db.ReimbursementClaims.AsNoTracking()
            .Include(x => x.Applicant)
            .Include(x => x.CurrentVersion)!.ThenInclude(x => x!.ExpenseItems)
                .ThenInclude(x => x.AttachmentLinks).ThenInclude(x => x.AttachmentAsset)
            .Include(x => x.CurrentVersion)!.ThenInclude(x => x!.TravelItinerary)
            .Include(x => x.CurrentVersion)!.ThenInclude(x => x!.MealAllowance)
            .Where(x => x.CurrentVersion != null
                && x.CurrentVersion.ProjectId == projectId
                && x.SubmittedAt != null
                && x.SubmittedAt >= fromInstant
                && x.SubmittedAt < toExclusive
                && x.Status != ClaimStatus.Draft)
            .OrderBy(x => x.SubmittedAt).ThenBy(x => x.ClaimNumber)
            .ToListAsync(cancellationToken);

        var summary = new List<object?[]>
        {
            new object?[] { "报销单号", "申请人", "个人姓名", "项目编码", "项目名称", "类型", "报销状态", "报销发放状态", "报销金额", "提交时间", "审核时间", "发放时间" }
        };
        var expenses = new List<object?[]> { new object?[] { "报销单号", "费用类别", "费用日期", "金额", "商户/承运方", "说明" } };
        var travel = new List<object?[]> { new object?[] { "报销单号", "出发地", "目的地", "出发日期", "返程日期" } };
        var meals = new List<object?[]> { new object?[] { "报销单号", "餐补天数", "每日金额", "餐补总额", "餐补状态", "餐补发放状态" } };

        foreach (var claim in claims)
        {
            var version = claim.CurrentVersion!;
            summary.Add([
                claim.ClaimNumber, claim.Applicant.DisplayName, claim.Applicant.PersonalName,
                version.ProjectCodeSnapshot, version.ProjectNameSnapshot, claim.Type.ToString(), claim.Status.ToString(), claim.PayoutStatus.ToString(),
                version.TotalAmount, FormatInstant(claim.SubmittedAt), FormatInstant(claim.ReviewedAt), FormatInstant(claim.PaidAt)
            ]);
            foreach (var item in version.ExpenseItems.OrderBy(x => x.ExpenseDate).ThenBy(x => x.Category))
                expenses.Add([claim.ClaimNumber, item.Category.ToString(), item.ExpenseDate?.ToString("yyyy-MM-dd"), item.Amount, item.Merchant, item.Note]);
            if (version.TravelItinerary is { } itinerary)
                travel.Add([claim.ClaimNumber, itinerary.DepartureLocation, itinerary.Destination, itinerary.DepartureDate?.ToString("yyyy-MM-dd"), itinerary.ReturnDate?.ToString("yyyy-MM-dd")]);
            if (version.MealAllowance is { } meal)
                meals.Add([claim.ClaimNumber, meal.Days, meal.DailyAmount, meal.TotalAmount, meal.Status.ToString(), meal.PayoutStatus.ToString()]);
        }
        summary.Add(CreateSummaryTotalRow(claims.Count, claims.Sum(claim => claim.CurrentVersion!.TotalAmount)));

        var content = XlsxWorkbookWriter.Write([
            new("报销汇总", summary),
            new("费用明细", expenses),
            new("行程明细", travel),
            new("餐补明细", meals)
        ]);
        var attachments = claims.SelectMany(claim => claim.CurrentVersion!.ExpenseItems.SelectMany(item => item.AttachmentLinks.Select(link =>
            new MonthlyClaimArchiveAttachment(
                claim.Applicant.PersonalName,
                item.Amount ?? 0m,
                link.AttachmentAsset.OriginalFileName,
                link.AttachmentAsset.ObjectKey))))
            .ToList();
        var safeCode = string.Concat(project.Code.Select(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_' ? ch : '_'));
        var workbookFileName = $"报销导出_{safeCode}_{from:yyyyMMdd}_{to:yyyyMMdd}.xlsx";
        return new MonthlyClaimExportPackage(content, workbookFileName, from, to, claims.Count, attachments);
    }

    private static string? FormatInstant(DateTimeOffset? value) => value?.ToOffset(TimeSpan.FromHours(8)).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

    internal static object?[] CreateSummaryTotalRow(int claimCount, decimal totalAmount) =>
        ["总计", $"共 {claimCount} 笔", null, null, null, null, null, null, totalAmount, null, null, null];

    private static DateOnly PreviousMonthDay(DateOnly date, int day)
    {
        var previous = date.AddMonths(-1);
        return new DateOnly(previous.Year, previous.Month, Math.Min(day, DateTime.DaysInMonth(previous.Year, previous.Month)));
    }

    private static TimeZoneInfo ChinaTimeZone()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById("Asia/Shanghai"); }
        catch (TimeZoneNotFoundException) { return TimeZoneInfo.FindSystemTimeZoneById("China Standard Time"); }
    }

    private sealed record MonthlyClaimExportPackage(byte[] Workbook, string WorkbookFileName, DateOnly From, DateOnly To, int ClaimCount, IReadOnlyList<MonthlyClaimArchiveAttachment> Attachments);
}

internal sealed record MonthlyClaimArchiveAttachment(string? PersonalName, decimal Amount, string OriginalFileName, string ObjectKey);

internal static class MonthlyClaimArchiveWriter
{
    private static readonly HashSet<char> InvalidNameCharacters = ['<', '>', ':', '"', '/', '\\', '|', '?', '*'];

    public static async Task WriteAsync(
        Stream destination,
        string workbookFileName,
        byte[] workbook,
        IReadOnlyList<MonthlyClaimArchiveAttachment> attachments,
        IPrivateFileStore fileStore,
        CancellationToken cancellationToken)
    {
        using var archive = new ZipArchive(destination, ZipArchiveMode.Create, leaveOpen: true);
        var workbookEntry = archive.CreateEntry(SanitizeSegment(workbookFileName, "报销导出.xlsx"), CompressionLevel.Fastest);
        await using (var workbookTarget = workbookEntry.Open())
            await workbookTarget.WriteAsync(workbook, cancellationToken);

        archive.CreateEntry("报销凭证/");
        var usedNames = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var attachment in attachments)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var fileName = CreateUniqueAttachmentName(attachment, usedNames);
            var entry = archive.CreateEntry($"报销凭证/{fileName}", CompressionLevel.Fastest);
            await using var source = await fileStore.OpenReadAsync(attachment.ObjectKey, cancellationToken);
            await using var target = entry.Open();
            await source.CopyToAsync(target, cancellationToken);
        }
    }

    internal static string CreateUniqueAttachmentName(MonthlyClaimArchiveAttachment attachment, IDictionary<string, int> usedNames)
    {
        var personalName = SanitizeSegment(attachment.PersonalName, "未填写姓名");
        var extension = Path.GetExtension(attachment.OriginalFileName);
        var baseName = $"{personalName}_{attachment.Amount.ToString("0.00", CultureInfo.InvariantCulture)}";
        var candidate = $"{baseName}{extension}";
        if (!usedNames.TryGetValue(candidate, out var count))
        {
            usedNames[candidate] = 1;
            return candidate;
        }

        count++;
        usedNames[candidate] = count;
        return $"{baseName}_{count}{extension}";
    }

    private static string SanitizeSegment(string? value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value)) return fallback;
        var sanitized = new string(value.Trim().Select(character =>
            character < ' ' || InvalidNameCharacters.Contains(character) ? '_' : character).ToArray()).Trim('.', ' ');
        return string.IsNullOrWhiteSpace(sanitized) ? fallback : sanitized;
    }
}

internal sealed record XlsxSheet(string Name, IReadOnlyList<object?[]> Rows);

internal static class XlsxWorkbookWriter
{
    public static byte[] Write(IReadOnlyList<XlsxSheet> sheets)
    {
        using var output = new MemoryStream();
        using (var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteXml(archive, "[Content_Types].xml", writer =>
            {
                writer.WriteStartElement("Types", "http://schemas.openxmlformats.org/package/2006/content-types");
                writer.WriteStartElement("Default"); writer.WriteAttributeString("Extension", "rels"); writer.WriteAttributeString("ContentType", "application/vnd.openxmlformats-package.relationships+xml"); writer.WriteEndElement();
                writer.WriteStartElement("Default"); writer.WriteAttributeString("Extension", "xml"); writer.WriteAttributeString("ContentType", "application/xml"); writer.WriteEndElement();
                writer.WriteStartElement("Override"); writer.WriteAttributeString("PartName", "/xl/workbook.xml"); writer.WriteAttributeString("ContentType", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"); writer.WriteEndElement();
                for (var index = 0; index < sheets.Count; index++)
                {
                    writer.WriteStartElement("Override"); writer.WriteAttributeString("PartName", $"/xl/worksheets/sheet{index + 1}.xml"); writer.WriteAttributeString("ContentType", "application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"); writer.WriteEndElement();
                }
                writer.WriteEndElement();
            });
            WriteXml(archive, "_rels/.rels", writer =>
            {
                writer.WriteStartElement("Relationships", "http://schemas.openxmlformats.org/package/2006/relationships");
                WriteRelationship(writer, "rId1", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument", "xl/workbook.xml");
                writer.WriteEndElement();
            });
            WriteXml(archive, "xl/workbook.xml", writer =>
            {
                writer.WriteStartElement("workbook", "http://schemas.openxmlformats.org/spreadsheetml/2006/main");
                writer.WriteAttributeString("xmlns", "r", null, "http://schemas.openxmlformats.org/officeDocument/2006/relationships");
                writer.WriteStartElement("sheets");
                for (var index = 0; index < sheets.Count; index++)
                {
                    writer.WriteStartElement("sheet"); writer.WriteAttributeString("name", sheets[index].Name); writer.WriteAttributeString("sheetId", (index + 1).ToString(CultureInfo.InvariantCulture)); writer.WriteAttributeString("r", "id", null, $"rId{index + 1}"); writer.WriteEndElement();
                }
                writer.WriteEndElement(); writer.WriteEndElement();
            });
            WriteXml(archive, "xl/_rels/workbook.xml.rels", writer =>
            {
                writer.WriteStartElement("Relationships", "http://schemas.openxmlformats.org/package/2006/relationships");
                for (var index = 0; index < sheets.Count; index++)
                    WriteRelationship(writer, $"rId{index + 1}", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet", $"worksheets/sheet{index + 1}.xml");
                writer.WriteEndElement();
            });
            for (var index = 0; index < sheets.Count; index++)
                WriteWorksheet(archive, index + 1, sheets[index].Rows);
        }
        return output.ToArray();
    }

    private static void WriteWorksheet(ZipArchive archive, int index, IReadOnlyList<object?[]> rows) => WriteXml(archive, $"xl/worksheets/sheet{index}.xml", writer =>
    {
        writer.WriteStartElement("worksheet", "http://schemas.openxmlformats.org/spreadsheetml/2006/main");
        writer.WriteStartElement("sheetData");
        for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            writer.WriteStartElement("row"); writer.WriteAttributeString("r", (rowIndex + 1).ToString(CultureInfo.InvariantCulture));
            for (var columnIndex = 0; columnIndex < rows[rowIndex].Length; columnIndex++)
            {
                var value = rows[rowIndex][columnIndex];
                writer.WriteStartElement("c"); writer.WriteAttributeString("r", $"{ColumnName(columnIndex + 1)}{rowIndex + 1}");
                if (value is decimal or int or long or double)
                {
                    writer.WriteStartElement("v"); writer.WriteString(Convert.ToString(value, CultureInfo.InvariantCulture) ?? "0"); writer.WriteEndElement();
                }
                else
                {
                    writer.WriteAttributeString("t", "inlineStr"); writer.WriteStartElement("is"); writer.WriteStartElement("t"); writer.WriteString(value?.ToString() ?? string.Empty); writer.WriteEndElement(); writer.WriteEndElement();
                }
                writer.WriteEndElement();
            }
            writer.WriteEndElement();
        }
        writer.WriteEndElement(); writer.WriteEndElement();
    });

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
}
