using Microsoft.EntityFrameworkCore;
using TravelReimbursement.Api.Data;
using TravelReimbursement.Api.Domain;

namespace TravelReimbursement.Api.Tests;

public sealed class BusinessModelTests
{
    [Fact]
    public void Weekly_report_is_unique_per_author_project_and_week()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=model_only;Username=model_only;Password=model_only")
            .Options;
        using var db = new AppDbContext(options);

        var entity = db.Model.FindEntityType(typeof(WeeklyReport));
        var uniqueIndex = entity!.GetIndexes().Single(index => index.IsUnique);

        Assert.Equal(new[] { nameof(WeeklyReport.AuthorId), nameof(WeeklyReport.ProjectId), nameof(WeeklyReport.WeekStart) }, uniqueIndex.Properties.Select(property => property.Name));
    }

    [Fact]
    public void Meeting_records_allow_multiple_entries_for_same_project_and_date()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=model_only;Username=model_only;Password=model_only")
            .Options;
        using var db = new AppDbContext(options);

        var entity = db.Model.FindEntityType(typeof(MeetingRecord));

        Assert.NotNull(entity);
        Assert.DoesNotContain(entity!.GetIndexes(), index => index.IsUnique);
        Assert.NotNull(entity.GetQueryFilter());
        Assert.NotNull(db.Model.FindEntityType(typeof(MeetingParticipant))!.GetQueryFilter());
        var itemEntity = db.Model.FindEntityType(typeof(MeetingRecordItem))!;
        Assert.NotNull(itemEntity.GetQueryFilter());
        Assert.Null(itemEntity.FindProperty("Kind"));
        var itemOrderIndex = itemEntity.GetIndexes().Single(index => index.IsUnique);
        Assert.Equal(new[] { nameof(MeetingRecordItem.MeetingRecordId), nameof(MeetingRecordItem.SortOrder) }, itemOrderIndex.Properties.Select(property => property.Name));
    }

    [Fact]
    public void Claim_archive_batch_has_unique_name_and_restricts_member_deletion()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=model_only;Username=model_only;Password=model_only")
            .Options;
        using var db = new AppDbContext(options);

        var batchEntity = db.Model.FindEntityType(typeof(ClaimArchiveBatch))!;
        var claimEntity = db.Model.FindEntityType(typeof(ReimbursementClaim))!;
        var normalizedNameIndex = batchEntity.GetIndexes().Single(index => index.IsUnique);
        var archiveForeignKey = claimEntity.GetForeignKeys().Single(key => key.PrincipalEntityType.ClrType == typeof(ClaimArchiveBatch));

        Assert.Equal(new[] { nameof(ClaimArchiveBatch.NormalizedName) }, normalizedNameIndex.Properties.Select(property => property.Name));
        Assert.Equal(nameof(ReimbursementClaim.ArchiveBatchId), archiveForeignKey.Properties.Single().Name);
        Assert.Equal(DeleteBehavior.Restrict, archiveForeignKey.DeleteBehavior);
        Assert.True(batchEntity.FindProperty(nameof(ClaimArchiveBatch.ConcurrencyToken))!.IsConcurrencyToken);
        Assert.True(claimEntity.FindProperty(nameof(ReimbursementClaim.ConcurrencyToken))!.IsConcurrencyToken);
    }

    [Fact]
    public void Payroll_model_has_month_employee_and_payout_uniqueness()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=model_only;Username=model_only;Password=model_only")
            .Options;
        using var db = new AppDbContext(options);

        var period = db.Model.FindEntityType(typeof(PayrollPeriod))!;
        var entry = db.Model.FindEntityType(typeof(PayrollEntry))!;
        var payout = db.Model.FindEntityType(typeof(PayrollPayoutRecord))!;
        var monthIndex = period.GetIndexes().Single(index => index.IsUnique);
        var employeeIndex = entry.GetIndexes().Single(index => index.IsUnique);
        var payoutIndex = payout.GetIndexes().Single(index => index.IsUnique);

        Assert.Equal(new[] { nameof(PayrollPeriod.PayrollMonth) }, monthIndex.Properties.Select(property => property.Name));
        Assert.Equal("\"Status\" <> 'Cancelled'", monthIndex.GetFilter());
        Assert.Equal(new[] { nameof(PayrollEntry.PayrollPeriodId), nameof(PayrollEntry.UserId) }, employeeIndex.Properties.Select(property => property.Name));
        Assert.Equal(new[] { nameof(PayrollPayoutRecord.PayrollEntryId) }, payoutIndex.Properties.Select(property => property.Name));
        Assert.True(period.FindProperty(nameof(PayrollPeriod.ConcurrencyToken))!.IsConcurrencyToken);
        Assert.True(entry.FindProperty(nameof(PayrollEntry.ConcurrencyToken))!.IsConcurrencyToken);
        Assert.Equal(DeleteBehavior.Restrict, entry.GetForeignKeys().Single(key => key.PrincipalEntityType.ClrType == typeof(PayrollPeriod)).DeleteBehavior);
        Assert.Equal(DeleteBehavior.Restrict, payout.GetForeignKeys().Single(key => key.PrincipalEntityType.ClrType == typeof(PayrollEntry)).DeleteBehavior);
    }
}
