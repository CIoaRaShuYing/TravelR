using System.ComponentModel.DataAnnotations;
using TravelReimbursement.Api.Domain;

namespace TravelReimbursement.Api.Contracts;

public sealed record RegisterRequest(
    [property: Required, StringLength(100)] string DisplayName,
    [property: Required, RegularExpression("^1[3-9]\\d{9}$")] string PhoneNumber,
    [property: Required, StringLength(100, MinimumLength = 8)] string Password);

public sealed record LoginRequest(
    [property: Required, RegularExpression("^1[3-9]\\d{9}$")] string PhoneNumber,
    [property: Required] string Password);

public sealed record ChangePasswordRequest(
    [property: Required] string CurrentPassword,
    [property: Required, StringLength(100, MinimumLength = 8)] string NewPassword);

public sealed record UpdateProfileRequest(
    [property: Required, StringLength(100, MinimumLength = 1)] string PersonalName,
    [property: Required, RegularExpression("^\\d{16,19}$")] string BankCardNumber);

public sealed record ResetPasswordRequest(
    [property: Required, StringLength(100, MinimumLength = 8)] string NewPassword);

public sealed record UpdateRegistrationModeRequest(RegistrationMode RegistrationMode);
public sealed record ReviewRegistrationRequest(Guid ConcurrencyToken);

public sealed record CreateProjectRequest(
    [property: Required, StringLength(50)] string Code,
    [property: Required, StringLength(200)] string Name,
    [property: StringLength(1000)] string? Description);

public sealed record UpdateProjectRequest(
    [property: Required, StringLength(200)] string Name,
    [property: StringLength(1000)] string? Description,
    Guid ConcurrencyToken);

public sealed record TravelItineraryDraftRequest(
    [property: StringLength(100)] string? DepartureLocation,
    [property: StringLength(100)] string? Destination,
    DateOnly? DepartureDate,
    DateOnly? ReturnDate);

public sealed record ExpenseItemDraftRequest(
    Guid ClientKey,
    ExpenseCategory Category,
    [property: Range(typeof(decimal), "0.01", "999999999")] decimal? Amount,
    DateOnly? ExpenseDate,
    [property: StringLength(200)] string? Merchant,
    [property: StringLength(500)] string? Note,
    List<Guid> AttachmentIds);

public sealed record CreateClaimRequest(
    ClaimType Type,
    Guid ProjectId,
    [property: StringLength(1000)] string? Description,
    TravelItineraryDraftRequest? TravelItinerary,
    List<ExpenseItemDraftRequest> ExpenseItems);

public sealed record CreateClaimVersionRequest(
    Guid ExpectedCurrentVersionId,
    Guid ConcurrencyToken,
    Guid ProjectId,
    [property: StringLength(1000)] string? Description,
    TravelItineraryDraftRequest? TravelItinerary,
    List<ExpenseItemDraftRequest> ExpenseItems);

public sealed record ClaimActionRequest(Guid ExpectedCurrentVersionId, Guid ConcurrencyToken);
public sealed record ReviewClaimRequest(Guid ExpectedCurrentVersionId, Guid ConcurrencyToken, [property: StringLength(1000)] string? Comment);
public sealed record ConfirmPayoutRequest(Guid ExpectedCurrentVersionId, Guid ConcurrencyToken, [property: StringLength(1000)] string? Note);

public sealed record ReviewMealAllowanceRequest(
    Guid ExpectedCurrentVersionId,
    Guid ClaimConcurrencyToken,
    Guid MealConcurrencyToken,
    [property: Range(typeof(decimal), "0.01", "999999999")] decimal? DailyAmount,
    [property: StringLength(1000)] string? Comment);

public sealed record ConfirmMealAllowancePayoutRequest(
    Guid ExpectedCurrentVersionId,
    Guid ClaimConcurrencyToken,
    Guid MealConcurrencyToken,
    [property: StringLength(1000)] string? Note);

public sealed record CreatePayrollPeriodRequest(DateOnly PayrollMonth);

public sealed record AddPayrollEntriesRequest(
    Guid PeriodConcurrencyToken,
    IReadOnlyList<Guid> UserIds);

public sealed record RemovePayrollEntryRequest(
    Guid PeriodConcurrencyToken,
    Guid EntryConcurrencyToken);

public sealed record SavePayrollEntryRequest(
    Guid Id,
    Guid EntryConcurrencyToken,
    [property: Range(typeof(decimal), "0", "999999999")] decimal BaseSalary,
    [property: Range(typeof(decimal), "0", "999999999")] decimal PerformanceSalary,
    [property: Range(typeof(decimal), "0", "999999999")] decimal Bonus,
    [property: Range(typeof(decimal), "0", "999999999")] decimal Allowance,
    [property: Range(typeof(decimal), "0", "999999999")] decimal OtherIncrease,
    [property: Range(typeof(decimal), "0", "999999999")] decimal SocialSecurityDeduction,
    [property: Range(typeof(decimal), "0", "999999999")] decimal HousingFundDeduction,
    [property: Range(typeof(decimal), "0", "999999999")] decimal IndividualIncomeTax,
    [property: Range(typeof(decimal), "0", "999999999")] decimal OtherDeduction,
    [property: StringLength(1000)] string? Note);

public sealed record SavePayrollEntriesRequest(
    Guid PeriodConcurrencyToken,
    IReadOnlyList<SavePayrollEntryRequest> Entries);

public sealed record PayrollPeriodActionRequest(Guid ConcurrencyToken);

public sealed record ConfirmPayrollPayoutRequest(
    Guid PeriodConcurrencyToken,
    Guid EntryConcurrencyToken,
    [property: StringLength(1000)] string? Note);

public sealed record ClaimArchiveRangeRequest(
    DateOnly SubmittedFrom,
    DateOnly SubmittedTo);

public sealed record CreateClaimArchiveBatchRequest(
    [property: Required, StringLength(200)] string Name,
    DateOnly SubmittedFrom,
    DateOnly SubmittedTo);

public sealed record RenameClaimArchiveBatchRequest(
    [property: Required, StringLength(200)] string Name,
    Guid ConcurrencyToken);

public sealed record CreateWeeklyReportRequest(
    Guid ProjectId,
    DateOnly WeekStart,
    [property: Required, StringLength(4000)] string CompletedWork,
    [property: Required, StringLength(4000)] string NextWeekPlan,
    [property: StringLength(4000)] string? Issues);

public sealed record UpdateWeeklyReportRequest(
    Guid ProjectId,
    DateOnly WeekStart,
    [property: Required, StringLength(4000)] string CompletedWork,
    [property: Required, StringLength(4000)] string NextWeekPlan,
    [property: StringLength(4000)] string? Issues,
    Guid ConcurrencyToken);

public sealed record MeetingParticipantRequest(
    [property: Required, StringLength(100)] string Name,
    [property: StringLength(200)] string? Organization,
    [property: StringLength(100)] string? Title,
    [property: StringLength(50)] string? Phone);

public sealed record MeetingRecordItemRequest(
    [property: Required, StringLength(4000)] string Content,
    [property: StringLength(100)] string? Status,
    DateOnly? DueDate,
    [property: StringLength(100)] string? Owner);

public sealed record CreateMeetingRecordRequest(
    Guid ProjectId,
    DateOnly MeetingDate,
    [property: Required, StringLength(200)] string Location,
    IReadOnlyList<MeetingParticipantRequest> Participants,
    IReadOnlyList<MeetingRecordItemRequest> Items);

public sealed record UpdateMeetingRecordRequest(
    Guid ProjectId,
    DateOnly MeetingDate,
    [property: Required, StringLength(200)] string Location,
    IReadOnlyList<MeetingParticipantRequest> Participants,
    IReadOnlyList<MeetingRecordItemRequest> Items,
    Guid ConcurrencyToken);

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int Total);
