using System.IO.Compression;
using System.Text;
using TravelReimbursement.Api.Domain;
using TravelReimbursement.Api.Services;

namespace TravelReimbursement.Api.Tests;

public sealed class XlsxWorkbookWriterTests
{
    [Fact]
    public void Workbook_contains_four_named_worksheets_and_valid_package_parts()
    {
        var content = XlsxWorkbookWriter.Write([
            new XlsxSheet("报销汇总", [new object?[] { "报销单号", "金额" }, new object?[] { "BX-001", 100m }]),
            new XlsxSheet("费用明细", [new object?[] { "报销单号" }]),
            new XlsxSheet("行程明细", [new object?[] { "报销单号" }]),
            new XlsxSheet("餐补明细", [new object?[] { "报销单号", "餐补天数" }])
        ]);

        using var archive = new ZipArchive(new MemoryStream(content), ZipArchiveMode.Read);
        var entryNames = archive.Entries.Select(entry => entry.FullName).ToHashSet();
        Assert.Contains("[Content_Types].xml", entryNames);
        Assert.Contains("xl/workbook.xml", entryNames);
        Assert.All(Enumerable.Range(1, 4), index => Assert.Contains($"xl/worksheets/sheet{index}.xml", entryNames));

        var workbook = ReadEntry(archive, "xl/workbook.xml");
        Assert.Contains("报销汇总", workbook, StringComparison.Ordinal);
        Assert.Contains("费用明细", workbook, StringComparison.Ordinal);
        Assert.Contains("行程明细", workbook, StringComparison.Ordinal);
        Assert.Contains("餐补明细", workbook, StringComparison.Ordinal);
    }

    [Fact]
    public void Summary_total_row_places_claim_count_and_amount_in_summary_columns()
    {
        var row = MonthlyClaimExportService.CreateSummaryTotalRow(3, 456.78m);

        Assert.Equal("总计", row[0]);
        Assert.Equal("共 3 笔", row[1]);
        Assert.Equal(456.78m, row[9]);
        Assert.Equal(13, row.Length);
    }

    [Fact]
    public void Summary_rows_include_claim_description_between_type_and_status()
    {
        var claim = new ReimbursementClaim
        {
            ClaimNumber = "BX-001",
            Applicant = new AppUser { DisplayName = "申请人", PersonalName = "张三" },
            Type = ClaimType.Travel,
            Status = ClaimStatus.Approved,
            PayoutStatus = PayoutStatus.Pending
        };
        var version = new ClaimVersion
        {
            ProjectCodeSnapshot = "P-001",
            ProjectNameSnapshot = "项目一",
            Description = "客户现场差旅",
            TotalAmount = 456.78m
        };

        var header = MonthlyClaimExportService.CreateSummaryHeaderRow();
        var row = MonthlyClaimExportService.CreateSummaryDataRow(claim, version);

        Assert.Equal("类型", header[5]);
        Assert.Equal("报销说明", header[6]);
        Assert.Equal("报销状态", header[7]);
        Assert.Equal("差旅行程", row[5]);
        Assert.Equal("客户现场差旅", row[6]);
        Assert.Equal("已批准", row[7]);
        Assert.Equal("待发放", row[8]);
        Assert.Equal(456.78m, row[9]);
        Assert.Equal(13, header.Length);
        Assert.Equal(header.Length, row.Length);
    }

    [Fact]
    public void Export_enum_labels_are_chinese()
    {
        Assert.Equal("差旅行程", MonthlyClaimExportService.ClaimTypeLabel(ClaimType.Travel));
        Assert.Equal("普通单据", MonthlyClaimExportService.ClaimTypeLabel(ClaimType.General));

        Assert.Equal("草稿", MonthlyClaimExportService.ClaimStatusLabel(ClaimStatus.Draft));
        Assert.Equal("待审批", MonthlyClaimExportService.ClaimStatusLabel(ClaimStatus.Submitted));
        Assert.Equal("已批准", MonthlyClaimExportService.ClaimStatusLabel(ClaimStatus.Approved));
        Assert.Equal("已驳回", MonthlyClaimExportService.ClaimStatusLabel(ClaimStatus.Rejected));
        Assert.Equal("已作废", MonthlyClaimExportService.ClaimStatusLabel(ClaimStatus.Cancelled));

        Assert.Equal("无需发放", MonthlyClaimExportService.PayoutStatusLabel(PayoutStatus.NotApplicable));
        Assert.Equal("待发放", MonthlyClaimExportService.PayoutStatusLabel(PayoutStatus.Pending));
        Assert.Equal("已发放", MonthlyClaimExportService.PayoutStatusLabel(PayoutStatus.Paid));
    }

    private static string ReadEntry(ZipArchive archive, string name)
    {
        using var reader = new StreamReader(archive.GetEntry(name)!.Open(), Encoding.UTF8);
        return reader.ReadToEnd();
    }
}
