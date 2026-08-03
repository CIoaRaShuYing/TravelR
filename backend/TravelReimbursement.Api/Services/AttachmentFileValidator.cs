namespace TravelReimbursement.Api.Services;

public sealed record AttachmentFileValidationResult(bool IsValid, string? ContentType, string? ErrorMessage)
{
    public static AttachmentFileValidationResult Success(string contentType) => new(true, contentType, null);
    public static AttachmentFileValidationResult Failure(string message) => new(false, null, message);
}

public static class AttachmentFileValidator
{
    public const long MaxFileSize = 10 * 1024 * 1024;

    public static async Task<AttachmentFileValidationResult> ValidateAsync(IFormFile file, CancellationToken cancellationToken = default)
    {
        if (file.Length <= 0)
            return AttachmentFileValidationResult.Failure("文件内容为空，请重新选择文件。");

        if (file.Length > MaxFileSize)
        {
            var sizeInMb = file.Length / 1024d / 1024d;
            return AttachmentFileValidationResult.Failure($"文件大小为 {sizeInMb:F1}MB，单个文件不能超过 10MB。");
        }

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        var header = new byte[8];
        await using var stream = file.OpenReadStream();
        var bytesRead = await stream.ReadAsync(header.AsMemory(), cancellationToken);

        var contentType = extension switch
        {
            ".pdf" when bytesRead >= 5 && header.AsSpan(0, 5).SequenceEqual("%PDF-"u8) => "application/pdf",
            ".jpg" or ".jpeg" when bytesRead >= 3 && header[0] == 0xff && header[1] == 0xd8 && header[2] == 0xff => "image/jpeg",
            ".png" when bytesRead >= 8 && header.AsSpan().SequenceEqual(new byte[] { 0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a }) => "image/png",
            _ => null
        };

        return contentType is null
            ? AttachmentFileValidationResult.Failure("文件类型不支持，或文件内容与扩展名不一致。仅支持 JPG、PNG 或 PDF 文件。")
            : AttachmentFileValidationResult.Success(contentType);
    }
}
