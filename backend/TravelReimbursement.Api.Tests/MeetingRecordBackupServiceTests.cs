using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using TravelReimbursement.Api.Services;

namespace TravelReimbursement.Api.Tests;

public sealed class MeetingRecordBackupServiceTests
{
    private static readonly TimeZoneInfo ChinaTimeZone = MeetingRecordBackupService.ResolveChinaTimeZone();

    [Fact]
    public void Latest_slot_on_weekday_uses_previous_sunday_and_prior_monday()
    {
        var slot = MeetingRecordBackupService.GetLatestDueSlot(
            new DateTimeOffset(2026, 9, 3, 2, 0, 0, TimeSpan.Zero),
            ChinaTimeZone);

        Assert.Equal(new DateOnly(2026, 8, 24), slot.WeekStart);
        Assert.Equal(new DateOnly(2026, 8, 30), slot.ScheduledDate);
        Assert.Equal(new DateTimeOffset(2026, 8, 29, 18, 0, 0, TimeSpan.Zero), slot.ScheduledForUtc);
    }

    [Theory]
    [InlineData(2026, 9, 5, 17, 59, 2026, 8, 30)]
    [InlineData(2026, 9, 5, 18, 0, 2026, 9, 6)]
    public void Sunday_two_am_boundary_is_exact(
        int year,
        int month,
        int day,
        int hour,
        int minute,
        int expectedYear,
        int expectedMonth,
        int expectedDay)
    {
        var slot = MeetingRecordBackupService.GetLatestDueSlot(
            new DateTimeOffset(year, month, day, hour, minute, 0, TimeSpan.Zero),
            ChinaTimeZone);

        Assert.Equal(new DateOnly(expectedYear, expectedMonth, expectedDay), slot.ScheduledDate);
    }

    [Fact]
    public void Archive_validation_accepts_matching_manifest_data_and_checksums()
    {
        var path = Path.Combine(Path.GetTempPath(), $"meeting-backup-{Guid.NewGuid():N}.zip");
        var manifest = "{\"formatVersion\":\"meeting-records-backup-manifest.v1\"}"u8.ToArray();
        var data = "{\"formatVersion\":\"meeting-records.v1\",\"records\":[]}"u8.ToArray();
        try
        {
            using (var archive = ZipFile.Open(path, ZipArchiveMode.Create))
            {
                WriteEntry(archive, "manifest.json", manifest);
                WriteEntry(archive, "meeting-records.v1.json", data);
                WriteEntry(archive, "checksums.sha256", Encoding.UTF8.GetBytes($"{Hash(manifest)}  manifest.json\n{Hash(data)}  meeting-records.v1.json\n"));
            }

            MeetingRecordBackupService.ValidateArchive(path);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    private static void WriteEntry(ZipArchive archive, string name, byte[] content)
    {
        using var stream = archive.CreateEntry(name).Open();
        stream.Write(content);
    }

    private static string Hash(byte[] content) => Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();
}
