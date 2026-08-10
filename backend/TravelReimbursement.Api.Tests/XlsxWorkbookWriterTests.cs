using System.IO.Compression;
using System.Text;
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

    private static string ReadEntry(ZipArchive archive, string name)
    {
        using var reader = new StreamReader(archive.GetEntry(name)!.Open(), Encoding.UTF8);
        return reader.ReadToEnd();
    }
}
