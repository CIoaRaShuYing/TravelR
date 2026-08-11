using System.IO.Compression;
using Microsoft.AspNetCore.Http;
using TravelReimbursement.Api.Services;

namespace TravelReimbursement.Api.Tests;

public sealed class MonthlyClaimArchiveWriterTests
{
    [Fact]
    public async Task Archive_contains_workbook_receipt_folder_and_personal_name_amount_files()
    {
        var fileStore = new FakePrivateFileStore(new Dictionary<string, byte[]>
        {
            ["first"] = "first-content"u8.ToArray(),
            ["second"] = "second-content"u8.ToArray(),
            ["third"] = "third-content"u8.ToArray()
        });
        var attachments = new[]
        {
            new MonthlyClaimArchiveAttachment("张/三", 123.4m, "invoice.pdf", "first"),
            new MonthlyClaimArchiveAttachment("张/三", 123.4m, "receipt.pdf", "second"),
            new MonthlyClaimArchiveAttachment("李四", 88m, "photo.PNG", "third")
        };
        await using var output = new MemoryStream();

        await MonthlyClaimArchiveWriter.WriteAsync(output, "报销导出_TEST.xlsx", "workbook"u8.ToArray(), attachments, fileStore, CancellationToken.None);

        output.Position = 0;
        using var archive = new ZipArchive(output, ZipArchiveMode.Read);
        Assert.Equal("workbook"u8.ToArray(), ReadEntry(archive, "报销导出_TEST.xlsx"));
        Assert.NotNull(archive.GetEntry("报销凭证/"));
        Assert.Equal("first-content"u8.ToArray(), ReadEntry(archive, "报销凭证/张_三_123.40.pdf"));
        Assert.Equal("second-content"u8.ToArray(), ReadEntry(archive, "报销凭证/张_三_123.40_2.pdf"));
        Assert.Equal("third-content"u8.ToArray(), ReadEntry(archive, "报销凭证/李四_88.00.PNG"));
    }

    [Fact]
    public void Attachment_name_uses_safe_fallback_when_personal_name_is_missing()
    {
        var usedNames = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        var name = MonthlyClaimArchiveWriter.CreateUniqueAttachmentName(
            new MonthlyClaimArchiveAttachment(null, 10m, "voucher.jpg", "object"), usedNames);

        Assert.Equal("未填写姓名_10.00.jpg", name);
    }

    [Fact]
    public async Task Archive_keeps_receipt_folder_when_no_attachments_exist()
    {
        await using var output = new MemoryStream();

        await MonthlyClaimArchiveWriter.WriteAsync(
            output, "报销导出_EMPTY.xlsx", "workbook"u8.ToArray(), [], new FakePrivateFileStore(new Dictionary<string, byte[]>()), CancellationToken.None);

        output.Position = 0;
        using var archive = new ZipArchive(output, ZipArchiveMode.Read);
        Assert.NotNull(archive.GetEntry("报销凭证/"));
    }

    [Fact]
    public async Task Archive_fails_when_attachment_file_is_missing()
    {
        await using var output = new MemoryStream();
        var attachment = new MonthlyClaimArchiveAttachment("王五", 20m, "missing.pdf", "missing");

        await Assert.ThrowsAsync<FileNotFoundException>(() => MonthlyClaimArchiveWriter.WriteAsync(
            output, "报销导出_TEST.xlsx", "workbook"u8.ToArray(), [attachment], new FakePrivateFileStore(new Dictionary<string, byte[]>()), CancellationToken.None));
    }

    private static byte[] ReadEntry(ZipArchive archive, string name)
    {
        using var source = archive.GetEntry(name)!.Open();
        using var output = new MemoryStream();
        source.CopyTo(output);
        return output.ToArray();
    }

    private sealed class FakePrivateFileStore(IReadOnlyDictionary<string, byte[]> files) : IPrivateFileStore
    {
        public Task<StoredFile> SaveAsync(IFormFile file, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<Stream> OpenReadAsync(string objectKey, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!files.TryGetValue(objectKey, out var content)) throw new FileNotFoundException();
            return Task.FromResult<Stream>(new MemoryStream(content, writable: false));
        }

        public Task DeleteAsync(string objectKey, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
