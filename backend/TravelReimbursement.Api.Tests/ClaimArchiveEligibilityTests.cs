using TravelReimbursement.Api.Domain;
using TravelReimbursement.Api.Services;

namespace TravelReimbursement.Api.Tests;

public sealed class ClaimArchiveEligibilityTests
{
    [Fact]
    public void Paid_claim_with_paid_meal_allowance_is_eligible()
    {
        var claim = Claim(ClaimStatus.Approved, PayoutStatus.Paid, MealAllowanceStatus.Approved, PayoutStatus.Paid);

        Assert.Null(ClaimArchiveEligibility.GetBlockingReason(claim));
    }

    [Theory]
    [InlineData(ClaimStatus.Rejected, MealAllowanceStatus.Rejected)]
    [InlineData(ClaimStatus.Cancelled, MealAllowanceStatus.Cancelled)]
    public void Final_non_paid_claim_and_meal_states_are_eligible(ClaimStatus claimStatus, MealAllowanceStatus mealStatus)
    {
        var claim = Claim(claimStatus, PayoutStatus.NotApplicable, mealStatus, PayoutStatus.NotApplicable);

        Assert.Null(ClaimArchiveEligibility.GetBlockingReason(claim));
    }

    [Theory]
    [InlineData(ClaimStatus.Submitted, PayoutStatus.NotApplicable)]
    [InlineData(ClaimStatus.Approved, PayoutStatus.Pending)]
    public void Active_claim_workflow_blocks_archive(ClaimStatus claimStatus, PayoutStatus payoutStatus)
    {
        var claim = Claim(claimStatus, payoutStatus, null, null);

        Assert.Equal("报销尚未完成审批和发放。", ClaimArchiveEligibility.GetBlockingReason(claim));
    }

    [Theory]
    [InlineData(MealAllowanceStatus.PendingTravelReview, PayoutStatus.NotApplicable)]
    [InlineData(MealAllowanceStatus.PendingReview, PayoutStatus.NotApplicable)]
    [InlineData(MealAllowanceStatus.Approved, PayoutStatus.Pending)]
    public void Active_meal_allowance_workflow_blocks_archive(MealAllowanceStatus mealStatus, PayoutStatus payoutStatus)
    {
        var claim = Claim(ClaimStatus.Approved, PayoutStatus.Paid, mealStatus, payoutStatus);

        Assert.Equal("餐补尚未完成审批和发放。", ClaimArchiveEligibility.GetBlockingReason(claim));
    }

    [Fact]
    public void Existing_archive_membership_blocks_archive()
    {
        var claim = Claim(ClaimStatus.Approved, PayoutStatus.Paid, null, null);
        claim.ArchiveBatchId = Guid.NewGuid();

        Assert.Equal("该报销已经归档。", ClaimArchiveEligibility.GetBlockingReason(claim));
    }

    private static ReimbursementClaim Claim(
        ClaimStatus claimStatus,
        PayoutStatus claimPayoutStatus,
        MealAllowanceStatus? mealStatus,
        PayoutStatus? mealPayoutStatus)
    {
        return new ReimbursementClaim
        {
            Status = claimStatus,
            PayoutStatus = claimPayoutStatus,
            CurrentVersion = new ClaimVersion
            {
                MealAllowance = mealStatus.HasValue
                    ? new MealAllowance
                    {
                        Status = mealStatus.Value,
                        PayoutStatus = mealPayoutStatus ?? PayoutStatus.NotApplicable
                    }
                    : null
            }
        };
    }
}
