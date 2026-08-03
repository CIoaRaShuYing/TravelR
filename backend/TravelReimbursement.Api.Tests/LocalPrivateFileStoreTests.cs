using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using TravelReimbursement.Api.Services;

namespace TravelReimbursement.Api.Tests;

public sealed class LocalPrivateFileStoreTests
{
    [Fact]
    public async Task Save_open_and_delete_round_trip_uses_configured_directory()
    {
        var contentRoot = CreateTemporaryDirectory();
        try
        {
            var store = CreateStore(contentRoot);
            var content = Encoding.ASCII.GetBytes("%PDF-1.4\nlocal-storage-test");
            await using var source = new MemoryStream(content);
            var file = new FormFile(source, 0, source.Length, "file", "receipt.pdf")
            {
                Headers = new HeaderDictionary(),
                ContentType = "application/pdf"
            };

            var stored = await store.SaveAsync(file, CancellationToken.None);

            Assert.Equal(content.Length, stored.Size);
            Assert.Equal(Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant(), stored.Sha256);
            Assert.EndsWith(".pdf", stored.ObjectKey);
            var expectedPath = Path.Combine(contentRoot, "attachments", stored.ObjectKey.Replace('/', Path.DirectorySeparatorChar));
            Assert.True(File.Exists(expectedPath));

            await using (var saved = await store.OpenReadAsync(stored.ObjectKey, CancellationToken.None))
            {
                using var copy = new MemoryStream();
                await saved.CopyToAsync(copy);
                Assert.Equal(content, copy.ToArray());
            }

            await store.DeleteAsync(stored.ObjectKey, CancellationToken.None);
            Assert.False(File.Exists(expectedPath));
        }
        finally
        {
            Directory.Delete(contentRoot, recursive: true);
        }
    }

    [Fact]
    public async Task Object_key_cannot_escape_storage_root()
    {
        var contentRoot = CreateTemporaryDirectory();
        try
        {
            var store = CreateStore(contentRoot);

            await Assert.ThrowsAsync<InvalidOperationException>(() => store.OpenReadAsync("../outside.pdf", CancellationToken.None));
        }
        finally
        {
            Directory.Delete(contentRoot, recursive: true);
        }
    }

    private static LocalPrivateFileStore CreateStore(string contentRoot) => new(
        new TestWebHostEnvironment(contentRoot),
        Options.Create(new StorageOptions { LocalPath = "attachments" }));

    private static string CreateTemporaryDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "chuchai-local-store-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private sealed class TestWebHostEnvironment(string contentRootPath) : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "TravelReimbursement.Api.Tests";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = contentRootPath;
        public string EnvironmentName { get; set; } = "Testing";
        public string ContentRootPath { get; set; } = contentRootPath;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
