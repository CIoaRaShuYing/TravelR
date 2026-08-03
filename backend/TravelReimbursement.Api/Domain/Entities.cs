using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace TravelReimbursement.Api.Domain;

public enum RegistrationMode { Open, ApprovalRequired, Closed }
public enum RegistrationRequestStatus { Pending, Approved, Rejected }
public enum ClaimType { Travel, General }
public enum ClaimStatus { Draft, Submitted, Approved, Rejected, Cancelled }
public enum PayoutStatus { NotApplicable, Pending, Paid }
public enum ExpenseCategory { DepartureTransport, ReturnTransport, Lodging, OfficeSupplies, Meal, Other, Unspecified }
public enum AttachmentScanStatus { Pending, Accepted, Rejected }
public enum AttachmentBindingStatus { Staged, Bound }

public sealed class AppUser : IdentityUser<Guid>
{
    public string DisplayName { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}

public sealed class SystemSettings
{
    public int Id { get; set; } = 1;
    public RegistrationMode RegistrationMode { get; set; } = RegistrationMode.ApprovalRequired;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public Guid? UpdatedById { get; set; }
}

public sealed class RegistrationRequest
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string DisplayName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public RegistrationRequestStatus Status { get; set; } = RegistrationRequestStatus.Pending;
    public Guid? ReviewedById { get; set; }
    public DateTimeOffset? ReviewedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    [ConcurrencyCheck]
    public Guid ConcurrencyToken { get; set; } = Guid.NewGuid();
}

public sealed class Project
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Code { get; set; } = string.Empty;
    public string NormalizedCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public Guid CreatedById { get; set; }
    public Guid UpdatedById { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    [ConcurrencyCheck]
    public Guid ConcurrencyToken { get; set; } = Guid.NewGuid();
    public List<ClaimVersion> ClaimVersions { get; set; } = [];
}

public sealed class ReimbursementClaim
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string ClaimNumber { get; set; } = string.Empty;
    public Guid ApplicantId { get; set; }
    public AppUser Applicant { get; set; } = null!;
    public ClaimType Type { get; set; }
    public Guid? CurrentVersionId { get; set; }
    public ClaimVersion? CurrentVersion { get; set; }
    public ClaimStatus Status { get; set; } = ClaimStatus.Draft;
    public PayoutStatus PayoutStatus { get; set; } = PayoutStatus.NotApplicable;
    [ConcurrencyCheck]
    public Guid ConcurrencyToken { get; set; } = Guid.NewGuid();
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? SubmittedAt { get; set; }
    public DateTimeOffset? ReviewedAt { get; set; }
    public DateTimeOffset? CancelledAt { get; set; }
    public DateTimeOffset? PaidAt { get; set; }
    public List<ClaimVersion> Versions { get; set; } = [];
    public List<ApprovalRecord> ApprovalRecords { get; set; } = [];
    public PayoutRecord? PayoutRecord { get; set; }
    public List<AttachmentAsset> AttachmentAssets { get; set; } = [];
}

public sealed class ClaimVersion
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ClaimId { get; set; }
    public ReimbursementClaim Claim { get; set; } = null!;
    public int VersionNumber { get; set; }
    public Guid ProjectId { get; set; }
    public Project Project { get; set; } = null!;
    public string ProjectCodeSnapshot { get; set; } = string.Empty;
    public string ProjectNameSnapshot { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public Guid CreatedById { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? SupersededAt { get; set; }
    public TravelItinerary? TravelItinerary { get; set; }
    public List<ExpenseItem> ExpenseItems { get; set; } = [];
    public List<ApprovalRecord> ApprovalRecords { get; set; } = [];
}

public sealed class TravelItinerary
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ClaimVersionId { get; set; }
    public ClaimVersion ClaimVersion { get; set; } = null!;
    public string? DepartureLocation { get; set; }
    public string? Destination { get; set; }
    public DateOnly? DepartureDate { get; set; }
    public DateOnly? ReturnDate { get; set; }
}

public sealed class ExpenseItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ClaimVersionId { get; set; }
    public ClaimVersion ClaimVersion { get; set; } = null!;
    public Guid ClientKey { get; set; }
    public ExpenseCategory Category { get; set; }
    public decimal? Amount { get; set; }
    public string Currency { get; set; } = "CNY";
    public DateOnly? ExpenseDate { get; set; }
    public string? Merchant { get; set; }
    public string? Note { get; set; }
    public List<ExpenseItemAttachment> AttachmentLinks { get; set; } = [];
}

public sealed class AttachmentAsset
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OwnerId { get; set; }
    public AppUser Owner { get; set; } = null!;
    public Guid? BoundClaimId { get; set; }
    public ReimbursementClaim? BoundClaim { get; set; }
    public string ObjectKey { get; set; } = string.Empty;
    public string OriginalFileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long Size { get; set; }
    public string Sha256 { get; set; } = string.Empty;
    public AttachmentScanStatus ScanStatus { get; set; } = AttachmentScanStatus.Accepted;
    public AttachmentBindingStatus BindingStatus { get; set; } = AttachmentBindingStatus.Staged;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public List<ExpenseItemAttachment> ExpenseItemLinks { get; set; } = [];
}

public sealed class ExpenseItemAttachment
{
    public Guid ExpenseItemId { get; set; }
    public ExpenseItem ExpenseItem { get; set; } = null!;
    public Guid AttachmentAssetId { get; set; }
    public AttachmentAsset AttachmentAsset { get; set; } = null!;
}

public sealed class ApprovalRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ClaimId { get; set; }
    public ReimbursementClaim Claim { get; set; } = null!;
    public Guid ClaimVersionId { get; set; }
    public ClaimVersion ClaimVersion { get; set; } = null!;
    public ClaimStatus FromStatus { get; set; }
    public ClaimStatus ToStatus { get; set; }
    public Guid ActorId { get; set; }
    public string? Comment { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class PayoutRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ClaimId { get; set; }
    public ReimbursementClaim Claim { get; set; } = null!;
    public Guid ApprovedVersionId { get; set; }
    public ClaimVersion ApprovedVersion { get; set; } = null!;
    public decimal Amount { get; set; }
    public Guid ConfirmedById { get; set; }
    public string? Note { get; set; }
    public DateTimeOffset ConfirmedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class AuditLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? ActorId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public string? Context { get; set; }
    public string TraceId { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
