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

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int Total);
