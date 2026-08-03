using Microsoft.AspNetCore.Http;
using TravelReimbursement.Api.Services;

namespace TravelReimbursement.Api.Tests;

public sealed class AttachmentFileValidatorTests
{
    [Fact]
    public async Task Uppercase_pdf_extension_and_generic_browser_mime_are_accepted()
    {
        var file = CreateFile("invoice.PDF", "application/octet-stream", "%PDF-1.7\n"u8.ToArray());

        var result = await AttachmentFileValidator.ValidateAsync(file);

        Assert.True(result.IsValid);
        Assert.Equal("application/pdf", result.ContentType);
    }

    [Fact]
    public async Task Uppercase_jpg_extension_is_accepted()
    {
        var file = CreateFile("ticket.JPG", "image/jpeg", [0xff, 0xd8, 0xff, 0xe0]);

        var result = await AttachmentFileValidator.ValidateAsync(file);

        Assert.True(result.IsValid);
        Assert.Equal("image/jpeg", result.ContentType);
    }

    [Fact]
    public async Task Extension_and_file_signature_must_match()
    {
        var file = CreateFile("renamed.pdf", "application/pdf", "not a pdf"u8.ToArray());

        var result = await AttachmentFileValidator.ValidateAsync(file);

        Assert.False(result.IsValid);
        Assert.Contains("文件内容与扩展名不一致", result.ErrorMessage);
    }

    [Fact]
    public async Task Oversized_file_returns_specific_size_error()
    {
        var file = new FormFile(Stream.Null, 0, AttachmentFileValidator.MaxFileSize + 1, "file", "large.pdf");

        var result = await AttachmentFileValidator.ValidateAsync(file);

        Assert.False(result.IsValid);
        Assert.Contains("不能超过 10MB", result.ErrorMessage);
    }

    private static FormFile CreateFile(string fileName, string contentType, byte[] content)
    {
        var stream = new MemoryStream(content);
        return new FormFile(stream, 0, content.Length, "file", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = contentType
        };
    }
}
