using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using TravelReimbursement.Api.Data;
using TravelReimbursement.Api.Services;

namespace TravelReimbursement.Api.Tests;

public sealed class WeeklyReportExportServiceTests
{
    [Fact]
    public void Export_rows_are_chronological_and_stable_with_complete_content()
    {
        var fullContent = new string('周', 4000);
        var createdAt = new DateTimeOffset(2026, 8, 3, 1, 2, 3, TimeSpan.Zero);
        var reports = new[]
        {
            Report(new Guid("00000000-0000-0000-0000-000000000004"), new DateOnly(2026, 8, 10), "王五", "B-002", "项目乙", "完成乙"),
            Report(new Guid("00000000-0000-0000-0000-000000000001"), new DateOnly(2026, 8, 3), "张三", "C-003", "项目丙", fullContent, createdAt),
            Report(new Guid("00000000-0000-0000-0000-000000000003"), new DateOnly(2026, 8, 10), "赵六", "A-001", "项目甲", "完成甲二"),
            Report(new Guid("00000000-0000-0000-0000-000000000002"), new DateOnly(2026, 8, 10), "李四", "A-001", "项目甲", "完成甲一")
        };

        var rows = WeeklyReportExportService.CreateRows(reports);

        Assert.Equal(5, rows.Count);
        Assert.Equal(new[] { "周开始", "周结束", "用户显示名称", "个人姓名", "项目编码", "项目名称", "本周完成情况", "下周计划", "问题/需协助事项", "最后编辑人", "创建时间", "更新时间" }, rows[0]);
        Assert.Equal("2026-08-03", rows[1][0]);
        Assert.Equal("2026-08-09", rows[1][1]);
        Assert.Equal("张三", rows[1][2]);
        Assert.Equal("C-003", rows[1][4]);
        Assert.Equal(fullContent, rows[1][6]);
        Assert.Equal("2026-08-03 09:02:03", rows[1][10]);
        Assert.Equal("李四", rows[2][2]);
        Assert.Equal("A-001", rows[2][4]);
        Assert.Equal("赵六", rows[3][2]);
        Assert.Equal("A-001", rows[3][4]);
        Assert.Equal("王五", rows[4][2]);
        Assert.Equal("B-002", rows[4][4]);
        Assert.All(rows.Skip(1), row => Assert.Equal(12, row.Length));
    }

    [Fact]
    public async Task Export_rejects_reversed_week_range_before_querying_database()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=model_only;Username=model_only;Password=model_only")
            .Options;
        await using var db = new AppDbContext(options);
        var service = new WeeklyReportExportService(db);

        var exception = await Assert.ThrowsAsync<ApiProblemException>(() => service.CreateAsync(
            null,
            null,
            new DateOnly(2026, 8, 10),
            new DateOnly(2026, 8, 3),
            CancellationToken.None));

        Assert.Equal(StatusCodes.Status400BadRequest, exception.StatusCode);
        Assert.Equal("WEEKLY_REPORT_EXPORT_DATE_INVALID", exception.Code);
    }

    private static WeeklyReportExportRow Report(
        Guid id,
        DateOnly weekStart,
        string author,
        string projectCode,
        string projectName,
        string completedWork,
        DateTimeOffset? createdAt = null) =>
        new(
            id,
            weekStart,
            author,
            $"{author}姓名",
            projectCode,
            projectName,
            completedWork,
            "下周计划",
            "需要协助",
            "最后编辑人",
            createdAt ?? new DateTimeOffset(2026, 8, 10, 1, 2, 3, TimeSpan.Zero),
            new DateTimeOffset(2026, 8, 10, 2, 3, 4, TimeSpan.Zero));
}
