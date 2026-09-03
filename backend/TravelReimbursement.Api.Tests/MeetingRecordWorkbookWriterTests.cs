using System.IO.Compression;
using System.Text;
using TravelReimbursement.Api.Services;

namespace TravelReimbursement.Api.Tests;

public sealed class MeetingRecordWorkbookWriterTests
{
    [Fact]
    public void Workbook_uses_one_template_sheet_per_record_with_stable_same_day_names()
    {
        var content = MeetingRecordWorkbookWriter.Write([
            Record(new Guid("00000000-0000-0000-0000-000000000002"), new DateOnly(2026, 9, 2), new DateTimeOffset(2026, 9, 2, 2, 0, 0, TimeSpan.Zero), "第二场"),
            Record(new Guid("00000000-0000-0000-0000-000000000001"), new DateOnly(2026, 9, 2), new DateTimeOffset(2026, 9, 2, 1, 0, 0, TimeSpan.Zero), "第一场"),
            Record(new Guid("00000000-0000-0000-0000-000000000003"), new DateOnly(2026, 9, 3), new DateTimeOffset(2026, 9, 3, 1, 0, 0, TimeSpan.Zero), "第三场")
        ]);

        using var archive = new ZipArchive(new MemoryStream(content), ZipArchiveMode.Read);
        var workbook = ReadEntry(archive, "xl/workbook.xml");
        Assert.Contains("20260902-01", workbook, StringComparison.Ordinal);
        Assert.Contains("20260902-02", workbook, StringComparison.Ordinal);
        Assert.Contains("20260903-01", workbook, StringComparison.Ordinal);
        Assert.Contains("_xlnm.Print_Area", workbook, StringComparison.Ordinal);

        var firstSheet = ReadEntry(archive, "xl/worksheets/sheet1.xml");
        Assert.Contains("会议记录", firstSheet, StringComparison.Ordinal);
        Assert.Contains("第一场", firstSheet, StringComparison.Ordinal);
        Assert.Contains("张三", firstSheet, StringComparison.Ordinal);
        Assert.Contains("需求内容", firstSheet, StringComparison.Ordinal);
        Assert.Contains("工作重点", firstSheet, StringComparison.Ordinal);
        Assert.Contains("orientation=\"portrait\"", firstSheet, StringComparison.Ordinal);
        Assert.Contains("fitToWidth=\"1\"", firstSheet, StringComparison.Ordinal);
        Assert.NotNull(archive.GetEntry("xl/styles.xml"));
    }

    [Fact]
    public void Workbook_expands_rows_for_dynamic_participants_and_items()
    {
        var record = Record(Guid.NewGuid(), new DateOnly(2026, 9, 3), DateTimeOffset.UtcNow, "会议室");
        record = record with
        {
            Participants = Enumerable.Range(1, 5).Select(index => new MeetingParticipantExportData($"人员{index}", null, null, null)).ToArray(),
            Requirements = Enumerable.Range(1, 4).Select(index => new MeetingRecordItemExportData($"需求{index}", "推进中", null, "负责人")).ToArray(),
            WorkFocuses = Enumerable.Range(1, 6).Select(index => new MeetingRecordItemExportData($"重点{index}", null, null, null)).ToArray()
        };

        var content = MeetingRecordWorkbookWriter.Write([record]);

        using var archive = new ZipArchive(new MemoryStream(content), ZipArchiveMode.Read);
        var sheet = ReadEntry(archive, "xl/worksheets/sheet1.xml");
        Assert.Contains("人员5", sheet, StringComparison.Ordinal);
        Assert.Contains("需求4", sheet, StringComparison.Ordinal);
        Assert.Contains("重点6", sheet, StringComparison.Ordinal);
        Assert.Contains("dimension ref=\"A1:I22\"", sheet, StringComparison.Ordinal);
    }

    private static MeetingRecordExportData Record(Guid id, DateOnly date, DateTimeOffset createdAt, string location) => new(
        id,
        date,
        location,
        createdAt,
        [new MeetingParticipantExportData("张三", "示例单位", "项目经理", "13800000000")],
        [new MeetingRecordItemExportData("确认接口范围", "已确认", date.AddDays(3), "李四")],
        [new MeetingRecordItemExportData("完成联调", "进行中", date.AddDays(5), "王五")]);

    private static string ReadEntry(ZipArchive archive, string name)
    {
        using var reader = new StreamReader(archive.GetEntry(name)!.Open(), Encoding.UTF8);
        return reader.ReadToEnd();
    }
}
