using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using TravelReimbursement.Api.Domain;

namespace TravelReimbursement.Api.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityDbContext<AppUser, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<SystemSettings> SystemSettings => Set<SystemSettings>();
    public DbSet<RegistrationRequest> RegistrationRequests => Set<RegistrationRequest>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<ClaimArchiveBatch> ClaimArchiveBatches => Set<ClaimArchiveBatch>();
    public DbSet<ReimbursementClaim> ReimbursementClaims => Set<ReimbursementClaim>();
    public DbSet<ClaimVersion> ClaimVersions => Set<ClaimVersion>();
    public DbSet<TravelItinerary> TravelItineraries => Set<TravelItinerary>();
    public DbSet<MealAllowance> MealAllowances => Set<MealAllowance>();
    public DbSet<MealAllowanceApprovalRecord> MealAllowanceApprovalRecords => Set<MealAllowanceApprovalRecord>();
    public DbSet<MealAllowancePayoutRecord> MealAllowancePayoutRecords => Set<MealAllowancePayoutRecord>();
    public DbSet<ExpenseItem> ExpenseItems => Set<ExpenseItem>();
    public DbSet<AttachmentAsset> AttachmentAssets => Set<AttachmentAsset>();
    public DbSet<ExpenseItemAttachment> ExpenseItemAttachments => Set<ExpenseItemAttachment>();
    public DbSet<ApprovalRecord> ApprovalRecords => Set<ApprovalRecord>();
    public DbSet<PayoutRecord> PayoutRecords => Set<PayoutRecord>();
    public DbSet<PayrollPeriod> PayrollPeriods => Set<PayrollPeriod>();
    public DbSet<PayrollEntry> PayrollEntries => Set<PayrollEntry>();
    public DbSet<PayrollPayoutRecord> PayrollPayoutRecords => Set<PayrollPayoutRecord>();
    public DbSet<WeeklyReport> WeeklyReports => Set<WeeklyReport>();
    public DbSet<MeetingRecord> MeetingRecords => Set<MeetingRecord>();
    public DbSet<MeetingParticipant> MeetingParticipants => Set<MeetingParticipant>();
    public DbSet<MeetingRecordItem> MeetingRecordItems => Set<MeetingRecordItem>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<SystemSettings>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.RegistrationMode).HasConversion<string>().HasMaxLength(32);
        });
        builder.Entity<RegistrationRequest>(entity =>
        {
            entity.HasIndex(x => new { x.PhoneNumber, x.Status });
            entity.HasIndex(x => x.PhoneNumber).IsUnique().HasFilter("\"Status\" = 'Pending'");
            entity.Property(x => x.PhoneNumber).HasMaxLength(11);
            entity.Property(x => x.DisplayName).HasMaxLength(100);
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
            entity.Property(x => x.ConcurrencyToken).IsConcurrencyToken();
        });
        builder.Entity<AppUser>(entity =>
        {
            entity.HasIndex(x => x.PhoneNumber).IsUnique();
            entity.Property(x => x.PhoneNumber).HasMaxLength(11);
            entity.Property(x => x.DisplayName).HasMaxLength(100);
            entity.Property(x => x.PersonalName).HasMaxLength(100);
            entity.Property(x => x.BankCardProtected).HasMaxLength(2048);
        });
        builder.Entity<Project>(entity =>
        {
            entity.HasIndex(x => x.NormalizedCode).IsUnique();
            entity.HasIndex(x => x.Name).IsUnique();
            entity.HasIndex(x => new { x.IsActive, x.Name });
            entity.Property(x => x.Code).HasMaxLength(50);
            entity.Property(x => x.NormalizedCode).HasMaxLength(50);
            entity.Property(x => x.Name).HasMaxLength(200);
            entity.Property(x => x.Description).HasMaxLength(1000);
            entity.Property(x => x.ConcurrencyToken).IsConcurrencyToken();
        });
        builder.Entity<ClaimArchiveBatch>(entity =>
        {
            entity.ToTable(table => table.HasCheckConstraint(
                "CK_ClaimArchiveBatches_SubmittedRange",
                "\"SubmittedTo\" >= \"SubmittedFrom\""));
            entity.HasIndex(x => x.NormalizedName).IsUnique();
            entity.HasIndex(x => x.CreatedAt);
            entity.Property(x => x.Name).HasMaxLength(200);
            entity.Property(x => x.NormalizedName).HasMaxLength(200);
            entity.Property(x => x.ConcurrencyToken).IsConcurrencyToken();
            entity.HasOne(x => x.CreatedBy).WithMany().HasForeignKey(x => x.CreatedById).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<ReimbursementClaim>(entity =>
        {
            entity.HasIndex(x => x.ClaimNumber).IsUnique();
            entity.HasIndex(x => x.CurrentVersionId).IsUnique();
            entity.HasIndex(x => new { x.ApplicantId, x.Status, x.UpdatedAt });
            entity.HasIndex(x => new { x.PayoutStatus, x.UpdatedAt });
            entity.HasIndex(x => new { x.ArchiveBatchId, x.SubmittedAt });
            entity.Property(x => x.ClaimNumber).HasMaxLength(32);
            entity.Property(x => x.Type).HasConversion<string>().HasMaxLength(16);
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
            entity.Property(x => x.PayoutStatus).HasConversion<string>().HasMaxLength(16);
            entity.Property(x => x.ConcurrencyToken).IsConcurrencyToken();
            entity.HasOne(x => x.Applicant).WithMany().HasForeignKey(x => x.ApplicantId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.ArchiveBatch).WithMany(x => x.Claims).HasForeignKey(x => x.ArchiveBatchId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.CurrentVersion).WithOne().HasForeignKey<ReimbursementClaim>(x => x.CurrentVersionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasMany(x => x.Versions).WithOne(x => x.Claim).HasForeignKey(x => x.ClaimId).OnDelete(DeleteBehavior.Cascade);
        });
        builder.Entity<ClaimVersion>(entity =>
        {
            entity.HasIndex(x => new { x.ClaimId, x.VersionNumber }).IsUnique();
            entity.HasIndex(x => new { x.ProjectId, x.CreatedAt });
            entity.Property(x => x.ProjectCodeSnapshot).HasMaxLength(50);
            entity.Property(x => x.ProjectNameSnapshot).HasMaxLength(200);
            entity.Property(x => x.Description).HasMaxLength(1000);
            entity.Property(x => x.TotalAmount).HasPrecision(18, 2);
            entity.HasOne(x => x.Project).WithMany(x => x.ClaimVersions).HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.TravelItinerary).WithOne(x => x.ClaimVersion).HasForeignKey<TravelItinerary>(x => x.ClaimVersionId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.MealAllowance).WithOne(x => x.ClaimVersion).HasForeignKey<MealAllowance>(x => x.ClaimVersionId).OnDelete(DeleteBehavior.Cascade);
        });
        builder.Entity<MealAllowance>(entity =>
        {
            entity.HasIndex(x => x.ClaimVersionId).IsUnique();
            entity.HasIndex(x => new { x.Status, x.UpdatedAt });
            entity.HasIndex(x => new { x.PayoutStatus, x.UpdatedAt });
            entity.Property(x => x.DailyAmount).HasPrecision(18, 2);
            entity.Property(x => x.TotalAmount).HasPrecision(18, 2);
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
            entity.Property(x => x.PayoutStatus).HasConversion<string>().HasMaxLength(16);
            entity.Property(x => x.ReviewComment).HasMaxLength(1000);
            entity.Property(x => x.ConcurrencyToken).IsConcurrencyToken();
        });
        builder.Entity<MealAllowanceApprovalRecord>(entity =>
        {
            entity.HasIndex(x => new { x.MealAllowanceId, x.CreatedAt });
            entity.Property(x => x.FromStatus).HasConversion<string>().HasMaxLength(32);
            entity.Property(x => x.ToStatus).HasConversion<string>().HasMaxLength(32);
            entity.Property(x => x.DailyAmount).HasPrecision(18, 2);
            entity.Property(x => x.TotalAmount).HasPrecision(18, 2);
            entity.Property(x => x.Comment).HasMaxLength(1000);
            entity.HasOne(x => x.MealAllowance).WithMany(x => x.ApprovalRecords).HasForeignKey(x => x.MealAllowanceId).OnDelete(DeleteBehavior.Cascade);
        });
        builder.Entity<MealAllowancePayoutRecord>(entity =>
        {
            entity.HasIndex(x => x.MealAllowanceId).IsUnique();
            entity.Property(x => x.Amount).HasPrecision(18, 2);
            entity.Property(x => x.RecipientName).HasMaxLength(100);
            entity.Property(x => x.BankCardLastFour).HasMaxLength(4);
            entity.Property(x => x.Note).HasMaxLength(1000);
            entity.HasOne(x => x.MealAllowance).WithOne(x => x.PayoutRecord).HasForeignKey<MealAllowancePayoutRecord>(x => x.MealAllowanceId).OnDelete(DeleteBehavior.Cascade);
        });
        builder.Entity<TravelItinerary>(entity =>
        {
            entity.HasIndex(x => x.ClaimVersionId).IsUnique();
            entity.Property(x => x.DepartureLocation).HasMaxLength(100);
            entity.Property(x => x.Destination).HasMaxLength(100);
        });
        builder.Entity<ExpenseItem>(entity =>
        {
            entity.HasIndex(x => new { x.ClaimVersionId, x.Category });
            entity.HasIndex(x => new { x.ClaimVersionId, x.ClientKey }).IsUnique();
            entity.Property(x => x.Category).HasConversion<string>().HasMaxLength(32);
            entity.Property(x => x.Amount).HasPrecision(18, 2);
            entity.Property(x => x.Currency).HasMaxLength(3);
            entity.Property(x => x.Merchant).HasMaxLength(200);
            entity.Property(x => x.Note).HasMaxLength(500);
        });
        builder.Entity<AttachmentAsset>(entity =>
        {
            entity.HasIndex(x => x.ObjectKey).IsUnique();
            entity.HasIndex(x => new { x.OwnerId, x.BindingStatus, x.CreatedAt });
            entity.Property(x => x.ObjectKey).HasMaxLength(512);
            entity.Property(x => x.OriginalFileName).HasMaxLength(255);
            entity.Property(x => x.ContentType).HasMaxLength(128);
            entity.Property(x => x.Sha256).HasMaxLength(64);
            entity.Property(x => x.Purpose).HasConversion<string>().HasMaxLength(32);
            entity.Property(x => x.ScanStatus).HasConversion<string>().HasMaxLength(16);
            entity.Property(x => x.BindingStatus).HasConversion<string>().HasMaxLength(16);
            entity.HasOne(x => x.Owner).WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.BoundClaim).WithMany(x => x.AttachmentAssets).HasForeignKey(x => x.BoundClaimId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<ExpenseItemAttachment>(entity =>
        {
            entity.HasKey(x => new { x.ExpenseItemId, x.AttachmentAssetId });
            entity.HasOne(x => x.ExpenseItem).WithMany(x => x.AttachmentLinks).HasForeignKey(x => x.ExpenseItemId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.AttachmentAsset).WithMany(x => x.ExpenseItemLinks).HasForeignKey(x => x.AttachmentAssetId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<ApprovalRecord>(entity =>
        {
            entity.HasIndex(x => new { x.ClaimId, x.ClaimVersionId, x.CreatedAt });
            entity.Property(x => x.FromStatus).HasConversion<string>().HasMaxLength(16);
            entity.Property(x => x.ToStatus).HasConversion<string>().HasMaxLength(16);
            entity.Property(x => x.Comment).HasMaxLength(1000);
            entity.HasOne(x => x.Claim).WithMany(x => x.ApprovalRecords).HasForeignKey(x => x.ClaimId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.ClaimVersion).WithMany(x => x.ApprovalRecords).HasForeignKey(x => x.ClaimVersionId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<PayoutRecord>(entity =>
        {
            entity.HasIndex(x => x.ClaimId).IsUnique();
            entity.Property(x => x.Amount).HasPrecision(18, 2);
            entity.Property(x => x.RecipientName).HasMaxLength(100);
            entity.Property(x => x.BankCardLastFour).HasMaxLength(4);
            entity.Property(x => x.Note).HasMaxLength(1000);
            entity.HasOne(x => x.Claim).WithOne(x => x.PayoutRecord).HasForeignKey<PayoutRecord>(x => x.ClaimId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.ApprovedVersion).WithMany().HasForeignKey(x => x.ApprovedVersionId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<PayrollPeriod>(entity =>
        {
            entity.ToTable(table => table.HasCheckConstraint(
                "CK_PayrollPeriods_PayrollMonth",
                "EXTRACT(DAY FROM \"PayrollMonth\") = 1"));
            entity.HasIndex(x => x.PayrollMonth).IsUnique().HasFilter("\"Status\" <> 'Cancelled'");
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
            entity.Property(x => x.ConcurrencyToken).IsConcurrencyToken();
            entity.HasOne(x => x.CreatedBy).WithMany().HasForeignKey(x => x.CreatedById).OnDelete(DeleteBehavior.Restrict);
            entity.HasMany(x => x.Entries).WithOne(x => x.PayrollPeriod).HasForeignKey(x => x.PayrollPeriodId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<PayrollEntry>(entity =>
        {
            entity.ToTable(table =>
            {
                table.HasCheckConstraint("CK_PayrollEntries_Amounts", "\"BaseSalary\" >= 0 AND \"PerformanceSalary\" >= 0 AND \"Bonus\" >= 0 AND \"Allowance\" >= 0 AND \"OtherIncrease\" >= 0 AND \"SocialSecurityDeduction\" >= 0 AND \"HousingFundDeduction\" >= 0 AND \"IndividualIncomeTax\" >= 0 AND \"OtherDeduction\" >= 0 AND \"GrossPay\" >= 0 AND \"TotalDeductions\" >= 0 AND \"NetPay\" >= 0");
                table.HasCheckConstraint("CK_PayrollEntries_GrossPay", "\"GrossPay\" = \"BaseSalary\" + \"PerformanceSalary\" + \"Bonus\" + \"Allowance\" + \"OtherIncrease\"");
                table.HasCheckConstraint("CK_PayrollEntries_TotalDeductions", "\"TotalDeductions\" = \"SocialSecurityDeduction\" + \"HousingFundDeduction\" + \"IndividualIncomeTax\" + \"OtherDeduction\"");
                table.HasCheckConstraint("CK_PayrollEntries_NetPay", "\"NetPay\" = \"GrossPay\" - \"TotalDeductions\"");
            });
            entity.HasIndex(x => new { x.PayrollPeriodId, x.UserId }).IsUnique();
            entity.HasIndex(x => new { x.PayrollPeriodId, x.PayoutStatus });
            entity.HasIndex(x => new { x.UserId, x.PayoutStatus, x.PayrollPeriodId });
            entity.Property(x => x.EmployeeDisplayNameSnapshot).HasMaxLength(100);
            entity.Property(x => x.EmployeePersonalNameSnapshot).HasMaxLength(100);
            entity.Property(x => x.BankCardLastFourSnapshot).HasMaxLength(4);
            entity.Property(x => x.BaseSalary).HasPrecision(18, 2);
            entity.Property(x => x.PerformanceSalary).HasPrecision(18, 2);
            entity.Property(x => x.Bonus).HasPrecision(18, 2);
            entity.Property(x => x.Allowance).HasPrecision(18, 2);
            entity.Property(x => x.OtherIncrease).HasPrecision(18, 2);
            entity.Property(x => x.SocialSecurityDeduction).HasPrecision(18, 2);
            entity.Property(x => x.HousingFundDeduction).HasPrecision(18, 2);
            entity.Property(x => x.IndividualIncomeTax).HasPrecision(18, 2);
            entity.Property(x => x.OtherDeduction).HasPrecision(18, 2);
            entity.Property(x => x.GrossPay).HasPrecision(18, 2);
            entity.Property(x => x.TotalDeductions).HasPrecision(18, 2);
            entity.Property(x => x.NetPay).HasPrecision(18, 2);
            entity.Property(x => x.Note).HasMaxLength(1000);
            entity.Property(x => x.PayoutStatus).HasConversion<string>().HasMaxLength(16);
            entity.Property(x => x.ConcurrencyToken).IsConcurrencyToken();
            entity.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.UpdatedBy).WithMany().HasForeignKey(x => x.UpdatedById).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<PayrollPayoutRecord>(entity =>
        {
            entity.ToTable(table => table.HasCheckConstraint(
                "CK_PayrollPayoutRecords_BankCardLastFour",
                "\"BankCardLastFour\" ~ '^[0-9]{4}$'"));
            entity.HasIndex(x => x.PayrollEntryId).IsUnique();
            entity.Property(x => x.Amount).HasPrecision(18, 2);
            entity.Property(x => x.RecipientName).HasMaxLength(100);
            entity.Property(x => x.BankCardLastFour).HasMaxLength(4);
            entity.Property(x => x.Note).HasMaxLength(1000);
            entity.HasOne(x => x.PayrollEntry).WithOne(x => x.PayoutRecord).HasForeignKey<PayrollPayoutRecord>(x => x.PayrollEntryId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<AppUser>().WithMany().HasForeignKey(x => x.ConfirmedById).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<WeeklyReport>(entity =>
        {
            entity.HasIndex(x => new { x.AuthorId, x.ProjectId, x.WeekStart }).IsUnique();
            entity.HasIndex(x => new { x.ProjectId, x.WeekStart });
            entity.Property(x => x.CompletedWork).HasMaxLength(4000);
            entity.Property(x => x.NextWeekPlan).HasMaxLength(4000);
            entity.Property(x => x.Issues).HasMaxLength(4000);
            entity.Property(x => x.ConcurrencyToken).IsConcurrencyToken();
            entity.HasOne(x => x.Author).WithMany().HasForeignKey(x => x.AuthorId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.LastEditedBy).WithMany().HasForeignKey(x => x.LastEditedById).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Project).WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<MeetingRecord>(entity =>
        {
            entity.ToTable(table => table.HasCheckConstraint(
                "CK_MeetingRecords_DeletedState",
                "(\"DeletedAt\" IS NULL AND \"DeletedById\" IS NULL) OR (\"DeletedAt\" IS NOT NULL AND \"DeletedById\" IS NOT NULL)"));
            entity.HasQueryFilter(x => x.DeletedAt == null);
            entity.HasIndex(x => new { x.ProjectId, x.MeetingDate, x.CreatedAt }).HasFilter("\"DeletedAt\" IS NULL");
            entity.HasIndex(x => new { x.MeetingDate, x.CreatedAt }).HasFilter("\"DeletedAt\" IS NULL");
            entity.Property(x => x.Location).HasMaxLength(200);
            entity.Property(x => x.ConcurrencyToken).IsConcurrencyToken();
            entity.HasOne(x => x.Project).WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.CreatedBy).WithMany().HasForeignKey(x => x.CreatedById).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.LastEditedBy).WithMany().HasForeignKey(x => x.LastEditedById).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.DeletedBy).WithMany().HasForeignKey(x => x.DeletedById).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<MeetingParticipant>(entity =>
        {
            entity.HasQueryFilter(x => x.MeetingRecord.DeletedAt == null);
            entity.HasIndex(x => new { x.MeetingRecordId, x.SortOrder }).IsUnique();
            entity.Property(x => x.Name).HasMaxLength(100);
            entity.Property(x => x.Organization).HasMaxLength(200);
            entity.Property(x => x.Title).HasMaxLength(100);
            entity.Property(x => x.Phone).HasMaxLength(50);
            entity.HasOne(x => x.MeetingRecord).WithMany(x => x.Participants).HasForeignKey(x => x.MeetingRecordId).OnDelete(DeleteBehavior.Cascade);
        });
        builder.Entity<MeetingRecordItem>(entity =>
        {
            entity.HasQueryFilter(x => x.MeetingRecord.DeletedAt == null);
            entity.HasIndex(x => new { x.MeetingRecordId, x.SortOrder }).IsUnique();
            entity.Property(x => x.Content).HasMaxLength(4000);
            entity.Property(x => x.Status).HasMaxLength(100);
            entity.Property(x => x.Owner).HasMaxLength(100);
            entity.HasOne(x => x.MeetingRecord).WithMany(x => x.Items).HasForeignKey(x => x.MeetingRecordId).OnDelete(DeleteBehavior.Cascade);
        });
        builder.Entity<AuditLog>(entity =>
        {
            entity.HasIndex(x => new { x.EntityType, x.EntityId, x.CreatedAt });
            entity.Property(x => x.Action).HasMaxLength(100);
            entity.Property(x => x.EntityType).HasMaxLength(100);
            entity.Property(x => x.EntityId).HasMaxLength(100);
            entity.Property(x => x.TraceId).HasMaxLength(100);
        });
    }
}
