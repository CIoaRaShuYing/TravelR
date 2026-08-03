using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TravelReimbursement.Api.Data;
using TravelReimbursement.Api.Domain;

namespace TravelReimbursement.Api.Services;

public sealed class StagedAttachmentCleanupService(
    IServiceScopeFactory scopeFactory,
    IOptions<StorageOptions> storageOptions,
    ILogger<StagedAttachmentCleanupService> logger) : BackgroundService
{
    private readonly TimeSpan _retention = TimeSpan.FromHours(Math.Max(storageOptions.Value.StagedRetentionHours, 1));

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await CleanupAsync(stoppingToken);
        using var timer = new PeriodicTimer(TimeSpan.FromHours(1));
        while (await timer.WaitForNextTickAsync(stoppingToken))
            await CleanupAsync(stoppingToken);
    }

    private async Task CleanupAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var fileStore = scope.ServiceProvider.GetRequiredService<IPrivateFileStore>();
            var expiresBefore = DateTimeOffset.UtcNow.Subtract(_retention);
            var expired = await db.AttachmentAssets
                .Where(asset => asset.BindingStatus == AttachmentBindingStatus.Staged && asset.CreatedAt < expiresBefore)
                .OrderBy(asset => asset.CreatedAt)
                .Take(100)
                .ToListAsync(cancellationToken);

            foreach (var asset in expired)
            {
                try
                {
                    await fileStore.DeleteAsync(asset.ObjectKey, cancellationToken);
                    db.AttachmentAssets.Remove(asset);
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    logger.LogWarning(exception, "清理暂存附件失败，AttachmentId: {AttachmentId}", asset.Id);
                }
            }

            if (db.ChangeTracker.HasChanges()) await db.SaveChangesAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "暂存附件清理任务执行失败。");
        }
    }
}
