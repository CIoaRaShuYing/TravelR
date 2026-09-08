using TravelReimbursement.Api.Domain;

namespace TravelReimbursement.Api.Services;

public static class ExpenseItemDashboardQuery
{
    public static IQueryable<ExpenseItem> Apply(
        IQueryable<ExpenseItem> query,
        ExpenseCategory? category,
        Guid? projectId,
        Guid? applicantId,
        DateOnly? expenseFrom,
        DateOnly? expenseTo)
    {
        query = query.Where(item =>
            item.ClaimVersion.Claim.CurrentVersionId == item.ClaimVersionId
            && item.Category != ExpenseCategory.Unspecified
            && (item.ClaimVersion.Claim.Status == ClaimStatus.Submitted
                || item.ClaimVersion.Claim.Status == ClaimStatus.Approved
                || item.ClaimVersion.Claim.Status == ClaimStatus.Rejected));
        if (category.HasValue)
            query = query.Where(item => item.Category == category.Value);
        if (projectId.HasValue)
            query = query.Where(item => item.ClaimVersion.ProjectId == projectId.Value);
        if (applicantId.HasValue)
            query = query.Where(item => item.ClaimVersion.Claim.ApplicantId == applicantId.Value);
        if (expenseFrom.HasValue)
            query = query.Where(item => item.ExpenseDate >= expenseFrom.Value);
        if (expenseTo.HasValue)
            query = query.Where(item => item.ExpenseDate <= expenseTo.Value);
        return query;
    }
}
