using TravelReimbursement.Api.Domain;

namespace TravelReimbursement.Api.Services;

public static class ClaimArchiveEligibility
{
    public static string? GetBlockingReason(ReimbursementClaim claim)
    {
        if (claim.ArchiveBatchId.HasValue)
            return "该报销已经归档。";

        if (!IsClaimSettled(claim))
            return "报销尚未完成审批和发放。";

        if (!IsMealAllowanceSettled(claim.CurrentVersion?.MealAllowance))
            return "餐补尚未完成审批和发放。";

        return null;
    }

    private static bool IsClaimSettled(ReimbursementClaim claim) =>
        claim.Status is ClaimStatus.Rejected or ClaimStatus.Cancelled
        || claim is { Status: ClaimStatus.Approved, PayoutStatus: PayoutStatus.Paid };

    private static bool IsMealAllowanceSettled(MealAllowance? mealAllowance) => mealAllowance is null
        || mealAllowance.Status is MealAllowanceStatus.Rejected or MealAllowanceStatus.Cancelled
        || mealAllowance is { Status: MealAllowanceStatus.Approved, PayoutStatus: PayoutStatus.Paid };
}
