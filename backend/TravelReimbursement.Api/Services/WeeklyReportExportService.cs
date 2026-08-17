using System.Globalization;
using Microsoft.EntityFrameworkCore;
using TravelReimbursement.Api.Data;

namespace TravelReimbursement.Api.Services;

public sealed record WeeklyReportExportResult(byte[] Content, string FileName, int ReportCount);

public sealed class WeeklyReportExportService(AppDbContext db)
{
    public async Task<WeeklyReportExportResult> CreateAsync(
        Guid? authorId,
        Guid? projectId,
        DateOnly? weekFrom,
        DateOnly? weekTo,
        CancellationToken cancellationToken)
    {
        if (weekFrom.HasValue && weekTo.HasValue && weekTo.Value < weekFrom.Value)
            throw new ApiProblemException(StatusCodes.Status400BadRequest, "WEEKLY_REPORT_EXPORT_DATE_INVALID", "导出结束日期不能早于开始日期。");

        var query = db.WeeklyReports.AsNoTracking().AsQueryable();
        if (authorId.HasValue) query = query.Where(report => report.AuthorId == authorId.Value);
        if (projectId.HasValue) query = query.Where(report => report.ProjectId == projectId.Value);
        if (weekFrom.HasValue) query = query.Where(report => report.WeekStart >= weekFrom.Value);
        if (weekTo.HasValue) query = query.Where(report => report.WeekStart <= weekTo.Value);

        var reports = await query.Select(report => new WeeklyReportExportRow(
                report.Id,
                report.WeekStart,
                report.Author.DisplayName,
                report.Author.PersonalName,
                report.Project.Code,
                report.Project.Name,
                report.CompletedWork,
                report.NextWeekPlan,
                report.Issues,
                report.LastEditedBy.DisplayName,
                report.CreatedAt,
                report.UpdatedAt))
            .ToListAsync(cancellationToken);

        var workbook = XlsxWorkbookWriter.Write([new XlsxSheet("项目周报", CreateRows(reports))]);
        var fromLabel = weekFrom?.ToString("yyyyMMdd", CultureInfo.InvariantCulture) ?? "全部";
        var toLabel = weekTo?.ToString("yyyyMMdd", CultureInfo.InvariantCulture) ?? "全部";
        return new WeeklyReportExportResult(workbook, $"项目周报_{fromLabel}_{toLabel}.xlsx", reports.Count);
    }

    internal static IReadOnlyList<object?[]> CreateRows(IEnumerable<WeeklyReportExportRow> reports)
    {
        var rows = new List<object?[]>
        {
            new object?[] { "周开始", "周结束", "用户显示名称", "个人姓名", "项目编码", "项目名称", "本周完成情况", "下周计划", "问题/需协助事项", "最后编辑人", "创建时间", "更新时间" }
        };
        rows.AddRange(reports
            .OrderBy(report => report.WeekStart)
            .ThenBy(report => report.ProjectCode, StringComparer.OrdinalIgnoreCase)
            .ThenBy(report => report.AuthorDisplayName, StringComparer.Ordinal)
            .ThenBy(report => report.Id)
            .Select(CreateDataRow));
        return rows;
    }

    private static object?[] CreateDataRow(WeeklyReportExportRow report) =>
    [
        report.WeekStart.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        report.WeekStart.AddDays(6).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        report.AuthorDisplayName,
        report.AuthorPersonalName,
        report.ProjectCode,
        report.ProjectName,
        report.CompletedWork,
        report.NextWeekPlan,
        report.Issues,
        report.LastEditedByDisplayName,
        FormatInstant(report.CreatedAt),
        FormatInstant(report.UpdatedAt)
    ];

    private static string FormatInstant(DateTimeOffset value) =>
        value.ToOffset(TimeSpan.FromHours(8)).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
}

internal sealed record WeeklyReportExportRow(
    Guid Id,
    DateOnly WeekStart,
    string AuthorDisplayName,
    string? AuthorPersonalName,
    string ProjectCode,
    string ProjectName,
    string CompletedWork,
    string NextWeekPlan,
    string? Issues,
    string LastEditedByDisplayName,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
