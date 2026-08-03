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
    public DbSet<ReimbursementClaim> ReimbursementClaims => Set<ReimbursementClaim>();
    public DbSet<ClaimVersion> ClaimVersions => Set<ClaimVersion>();
    public DbSet<TravelItinerary> TravelItineraries => Set<TravelItinerary>();
    public DbSet<ExpenseItem> ExpenseItems => Set<ExpenseItem>();
    public DbSet<AttachmentAsset> AttachmentAssets => Set<AttachmentAsset>();
    public DbSet<ExpenseItemAttachment> ExpenseItemAttachments => Set<ExpenseItemAttachment>();
    public DbSet<ApprovalRecord> ApprovalRecords => Set<ApprovalRecord>();
    public DbSet<PayoutRecord> PayoutRecords => Set<PayoutRecord>();
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
        builder.Entity<ReimbursementClaim>(entity =>
        {
            entity.HasIndex(x => x.ClaimNumber).IsUnique();
            entity.HasIndex(x => x.CurrentVersionId).IsUnique();
            entity.HasIndex(x => new { x.ApplicantId, x.Status, x.UpdatedAt });
            entity.HasIndex(x => new { x.PayoutStatus, x.UpdatedAt });
            entity.Property(x => x.ClaimNumber).HasMaxLength(32);
            entity.Property(x => x.Type).HasConversion<string>().HasMaxLength(16);
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
            entity.Property(x => x.PayoutStatus).HasConversion<string>().HasMaxLength(16);
            entity.Property(x => x.ConcurrencyToken).IsConcurrencyToken();
            entity.HasOne(x => x.Applicant).WithMany().HasForeignKey(x => x.ApplicantId).OnDelete(DeleteBehavior.Restrict);
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
            entity.Property(x => x.Note).HasMaxLength(1000);
            entity.HasOne(x => x.Claim).WithOne(x => x.PayoutRecord).HasForeignKey<PayoutRecord>(x => x.ClaimId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.ApprovedVersion).WithMany().HasForeignKey(x => x.ApprovedVersionId).OnDelete(DeleteBehavior.Restrict);
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
