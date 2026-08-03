using System.Security.Cryptography;
using Microsoft.Extensions.Options;

namespace TravelReimbursement.Api.Services;

public interface IPrivateFileStore
{
    Task<StoredFile> SaveAsync(IFormFile file, CancellationToken cancellationToken);
    Task<Stream> OpenReadAsync(string objectKey, CancellationToken cancellationToken);
    Task DeleteAsync(string objectKey, CancellationToken cancellationToken);
}

public sealed record StoredFile(string ObjectKey, string Sha256, long Size);

public sealed class StorageOptions
{
    public string LocalPath { get; set; } = "private-uploads";
    public int StagedRetentionHours { get; set; } = 24;
}

public sealed class LocalPrivateFileStore : IPrivateFileStore
{
    private readonly string _root;

    public LocalPrivateFileStore(IWebHostEnvironment environment, IOptions<StorageOptions> options)
    {
        var configuredPath = options.Value.LocalPath?.Trim();
        if (string.IsNullOrWhiteSpace(configuredPath))
            throw new InvalidOperationException("缺少 FileStorage:LocalPath 配置。");

        _root = Path.GetFullPath(Path.IsPathRooted(configuredPath)
            ? configuredPath
            : Path.Combine(environment.ContentRootPath, configuredPath));
        EnsureWritable();
    }

    public async Task<StoredFile> SaveAsync(IFormFile file, CancellationToken cancellationToken)
    {
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        var objectKey = $"{DateTime.UtcNow:yyyy/MM}/{Guid.NewGuid():N}{extension}";
        var fullPath = ResolveObjectPath(objectKey);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

        await using var target = new FileStream(fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, FileOptions.Asynchronous);
        await using var source = file.OpenReadStream();
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[81920];
        int read;
        while ((read = await source.ReadAsync(buffer, cancellationToken)) > 0)
        {
            await target.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            hash.AppendData(buffer, 0, read);
        }

        return new StoredFile(objectKey, Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant(), file.Length);
    }

    public Task<Stream> OpenReadAsync(string objectKey, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var path = ResolveObjectPath(objectKey);
        if (!File.Exists(path)) throw new FileNotFoundException("附件不存在。");
        return Task.FromResult<Stream>(File.OpenRead(path));
    }

    public Task DeleteAsync(string objectKey, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var path = ResolveObjectPath(objectKey);
        if (File.Exists(path)) File.Delete(path);
        return Task.CompletedTask;
    }

    private string ResolveObjectPath(string objectKey)
    {
        if (string.IsNullOrWhiteSpace(objectKey))
            throw new InvalidOperationException("附件对象键不合法。");

        var path = Path.GetFullPath(Path.Combine(_root, objectKey.Replace('/', Path.DirectorySeparatorChar)));
        var relativePath = Path.GetRelativePath(_root, path);
        if (Path.IsPathRooted(relativePath)
            || relativePath.Equals("..", StringComparison.Ordinal)
            || relativePath.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            throw new InvalidOperationException("附件对象键不合法。");
        return path;
    }

    private void EnsureWritable()
    {
        string? probePath = null;
        try
        {
            Directory.CreateDirectory(_root);
            probePath = Path.Combine(_root, $".write-probe-{Guid.NewGuid():N}.tmp");
            using var probe = new FileStream(probePath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            probe.WriteByte(0);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new InvalidOperationException($"本地附件目录不可写：{_root}", exception);
        }
        finally
        {
            if (probePath is not null && File.Exists(probePath)) File.Delete(probePath);
        }
    }
}
