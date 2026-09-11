using System.Data;
using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TravelReimbursement.Api.Data;
using TravelReimbursement.Api.Domain;

namespace TravelReimbursement.Api.Services;

public sealed class MeetingRecordBackupOptions
{
    public bool Enabled { get; set; } = true;
    public string LocalPath { get; set; } = "../../meeting-record-backups";
    public int RetryMinutes { get; set; } = 15;
}

public sealed record MeetingRecordBackupRunResult(bool Created, string FilePath, int RecordCount, MeetingRecordBackupSlot Slot);
public sealed record MeetingRecordBackupSlot(DateOnly WeekStart, DateOnly ScheduledDate, DateTimeOffset ScheduledForUtc);

public sealed class MeetingRecordBackupService(
    AppDbContext db,
    IWebHostEnvironment environment,
    IOptions<MeetingRecordBackupOptions> options,
    TimeProvider timeProvider,
    ILogger<MeetingRecordBackupService> logger)
{
    private const string DataFileName = "meeting-records.v2.json";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private readonly MeetingRecordBackupOptions _options = options.Value;

    public async Task<MeetingRecordBackupRunResult?> RunIfDueAsync(CancellationToken cancellationToken)
    {
        if (!_options.Enabled) return null;

        var slot = GetLatestDueSlot(timeProvider.GetUtcNow(), ResolveChinaTimeZone());
        var root = ResolveRootPath(environment.ContentRootPath, _options.LocalPath);
        var directory = Path.Combine(root, slot.ScheduledDate.Year.ToString("0000", CultureInfo.InvariantCulture));
        var finalPath = Path.Combine(directory, $"meeting-records-full-week-{slot.WeekStart:yyyy-MM-dd}.zip");
        if (File.Exists(finalPath)) return new MeetingRecordBackupRunResult(false, finalPath, 0, slot);

        Directory.CreateDirectory(directory);
        var temporaryPath = $"{finalPath}.{Guid.NewGuid():N}.tmp";
        try
        {
            var capturedAtUtc = timeProvider.GetUtcNow();
            MeetingRecordBackupDocument document;
            await using (var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken))
            {
                var records = await db.MeetingRecords.IgnoreQueryFilters().AsNoTracking().AsSplitQuery()
                    .Include(record => record.Project)
                    .Include(record => record.CreatedBy)
                    .Include(record => record.LastEditedBy)
                    .Include(record => record.DeletedBy)
                    .Include(record => record.Participants)
                    .Include(record => record.Items)
                    .OrderBy(record => record.MeetingDate)
                    .ThenBy(record => record.CreatedAt)
                    .ThenBy(record => record.Id)
                    .ToListAsync(cancellationToken);

                document = new MeetingRecordBackupDocument(
                    "meeting-records.v2",
                    capturedAtUtc,
                    records.Select(ToBackupRecord).ToArray());
                await transaction.CommitAsync(cancellationToken);
            }

            var data = JsonSerializer.SerializeToUtf8Bytes(document, JsonOptions);
            var dataHash = Sha256(data);
            var manifest = JsonSerializer.SerializeToUtf8Bytes(new MeetingRecordBackupManifest(
                "meeting-records-backup-manifest.v2",
                slot.WeekStart,
                slot.ScheduledDate,
                slot.ScheduledForUtc,
                capturedAtUtc,
                document.Records.Count,
                DataFileName,
                dataHash), JsonOptions);
            var checksums = Encoding.UTF8.GetBytes($"{Sha256(manifest)}  manifest.json\n{dataHash}  {DataFileName}\n");

            await WriteArchiveAsync(temporaryPath, manifest, data, checksums, cancellationToken);
            ValidateArchive(temporaryPath);
            try
            {
                File.Move(temporaryPath, finalPath, overwrite: false);
            }
            catch (IOException) when (File.Exists(finalPath))
            {
                File.Delete(temporaryPath);
                return new MeetingRecordBackupRunResult(false, finalPath, document.Records.Count, slot);
            }

            db.AuditLogs.Add(new AuditLog
            {
                Action = "MeetingRecordsFullBackupCreated",
                EntityType = nameof(MeetingRecord),
                EntityId = slot.WeekStart.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                TraceId = "meeting-record-backup",
                Context = JsonSerializer.Serialize(new
                {
                    slot.WeekStart,
                    slot.ScheduledDate,
                    recordCount = document.Records.Count,
                    fileName = Path.GetFileName(finalPath),
                    sha256 = await Sha256FileAsync(finalPath, cancellationToken)
                })
            });
            await db.SaveChangesAsync(cancellationToken);
            logger.LogInformation("会议记录全量备份已生成：{BackupPath}，记录数：{RecordCount}", finalPath, document.Records.Count);
            return new MeetingRecordBackupRunResult(true, finalPath, document.Records.Count, slot);
        }
        catch
        {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
            throw;
        }
    }

    internal static MeetingRecordBackupSlot GetLatestDueSlot(DateTimeOffset utcNow, TimeZoneInfo timeZone)
    {
        var localNow = TimeZoneInfo.ConvertTime(utcNow, timeZone);
        var localDate = DateOnly.FromDateTime(localNow.DateTime);
        var scheduledDate = localDate.AddDays(-(int)localNow.DayOfWeek);
        if (localNow.DayOfWeek == DayOfWeek.Sunday && localNow.TimeOfDay < TimeSpan.FromHours(2))
            scheduledDate = scheduledDate.AddDays(-7);

        var scheduledLocal = DateTime.SpecifyKind(scheduledDate.ToDateTime(new TimeOnly(2, 0)), DateTimeKind.Unspecified);
        var scheduledForUtc = new DateTimeOffset(scheduledLocal, timeZone.GetUtcOffset(scheduledLocal)).ToUniversalTime();
        return new MeetingRecordBackupSlot(scheduledDate.AddDays(-6), scheduledDate, scheduledForUtc);
    }

    internal static TimeZoneInfo ResolveChinaTimeZone()
    {
        foreach (var id in new[] { "Asia/Shanghai", "China Standard Time" })
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
            catch (TimeZoneNotFoundException) { }
            catch (InvalidTimeZoneException) { }
        }
        throw new InvalidOperationException("服务器缺少中国标准时区定义。");
    }

    internal static void ValidateArchive(string path)
    {
        using var archive = ZipFile.OpenRead(path);
        var manifest = ReadRequiredEntry(archive, "manifest.json");
        var data = ReadRequiredEntry(archive, DataFileName);
        var checksums = Encoding.UTF8.GetString(ReadRequiredEntry(archive, "checksums.sha256"));
        if (!checksums.Contains($"{Sha256(manifest)}  manifest.json", StringComparison.Ordinal)
            || !checksums.Contains($"{Sha256(data)}  {DataFileName}", StringComparison.Ordinal))
            throw new InvalidDataException("会议记录备份校验和不匹配。");
    }

    private static MeetingRecordBackupEntry ToBackupRecord(MeetingRecord record) => new(
        record.Id,
        record.ProjectId,
        record.Project.Code,
        record.Project.Name,
        record.MeetingDate,
        record.Location,
        record.CreatedById,
        record.CreatedBy.DisplayName,
        record.LastEditedById,
        record.LastEditedBy.DisplayName,
        record.DeletedById,
        record.DeletedBy?.DisplayName,
        record.CreatedAt,
        record.UpdatedAt,
        record.DeletedAt,
        record.ConcurrencyToken,
        record.Participants.OrderBy(participant => participant.SortOrder)
            .Select(participant => new MeetingParticipantBackupEntry(participant.Id, participant.SortOrder, participant.Name, participant.Organization, participant.Title, participant.Phone))
            .ToArray(),
        record.Items.OrderBy(item => item.SortOrder).Select(ToBackupItem).ToArray());

    private static MeetingRecordItemBackupEntry ToBackupItem(MeetingRecordItem item) =>
        new(item.Id, item.SortOrder, item.Content, item.Status, item.DueDate, item.Owner);

    private static string ResolveRootPath(string contentRoot, string configuredPath) =>
        Path.GetFullPath(Path.IsPathRooted(configuredPath) ? configuredPath : Path.Combine(contentRoot, configuredPath));

    internal static async Task WriteArchiveAsync(string path, byte[] manifest, byte[] data, byte[] checksums, CancellationToken cancellationToken)
    {
        await using var target = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, FileOptions.Asynchronous | FileOptions.WriteThrough);
        using var archive = new ZipArchive(target, ZipArchiveMode.Create, leaveOpen: true);
        await WriteEntryAsync(archive, "manifest.json", manifest, cancellationToken);
        await WriteEntryAsync(archive, DataFileName, data, cancellationToken);
        await WriteEntryAsync(archive, "checksums.sha256", checksums, cancellationToken);
    }

    private static async Task WriteEntryAsync(ZipArchive archive, string name, byte[] content, CancellationToken cancellationToken)
    {
        var entry = archive.CreateEntry(name, CompressionLevel.Optimal);
        await using var stream = entry.Open();
        await stream.WriteAsync(content, cancellationToken);
    }

    private static byte[] ReadRequiredEntry(ZipArchive archive, string name)
    {
        var entry = archive.GetEntry(name) ?? throw new InvalidDataException($"会议记录备份缺少 {name}。");
        using var source = entry.Open();
        using var output = new MemoryStream();
        source.CopyTo(output);
        return output.ToArray();
    }

    private static string Sha256(byte[] content) => Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();

    private static async Task<string> Sha256FileAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        return Convert.ToHexString(await SHA256.HashDataAsync(stream, cancellationToken)).ToLowerInvariant();
    }
}

public sealed class MeetingRecordBackupHostedService(
    IServiceScopeFactory scopeFactory,
    IOptions<MeetingRecordBackupOptions> options,
    ILogger<MeetingRecordBackupHostedService> logger) : BackgroundService
{
    private readonly TimeSpan _interval = TimeSpan.FromMinutes(Math.Max(options.Value.RetryMinutes, 1));

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RunAsync(stoppingToken);
        using var timer = new PeriodicTimer(_interval);
        while (await timer.WaitForNextTickAsync(stoppingToken)) await RunAsync(stoppingToken);
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<MeetingRecordBackupService>().RunIfDueAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "会议记录全量备份失败，将在下一检查周期重试。");
        }
    }
}

internal sealed record MeetingRecordBackupManifest(
    string FormatVersion,
    DateOnly WeekStart,
    DateOnly ScheduledDate,
    DateTimeOffset ScheduledForUtc,
    DateTimeOffset CapturedAtUtc,
    int RecordCount,
    string DataFile,
    string DataSha256);

internal sealed record MeetingRecordBackupDocument(
    string FormatVersion,
    DateTimeOffset CapturedAtUtc,
    IReadOnlyList<MeetingRecordBackupEntry> Records);

internal sealed record MeetingRecordBackupEntry(
    Guid Id,
    Guid ProjectId,
    string ProjectCode,
    string ProjectName,
    DateOnly MeetingDate,
    string Location,
    Guid CreatedById,
    string CreatedByDisplayName,
    Guid LastEditedById,
    string LastEditedByDisplayName,
    Guid? DeletedById,
    string? DeletedByDisplayName,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? DeletedAt,
    Guid ConcurrencyToken,
    IReadOnlyList<MeetingParticipantBackupEntry> Participants,
    IReadOnlyList<MeetingRecordItemBackupEntry> Items);

internal sealed record MeetingParticipantBackupEntry(Guid Id, int SortOrder, string Name, string? Organization, string? Title, string? Phone);
internal sealed record MeetingRecordItemBackupEntry(Guid Id, int SortOrder, string Content, string? Status, DateOnly? DueDate, string? Owner);
