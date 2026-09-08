using TravelReimbursement.Api.Domain;

namespace TravelReimbursement.Api.Services;

public static class MealAllowanceLedgerQuery
{
    public static IQueryable<ReimbursementClaim> Apply(
        IQueryable<ReimbursementClaim> query,
        Guid? projectId,
        Guid? applicantId,
        DateOnly? tripFrom,
        DateOnly? tripTo)
    {
        query = query.Where(claim => claim.CurrentVersion != null && claim.CurrentVersion.MealAllowance != null);
        if (projectId.HasValue)
            query = query.Where(claim => claim.CurrentVersion!.ProjectId == projectId.Value);
        if (applicantId.HasValue)
            query = query.Where(claim => claim.ApplicantId == applicantId.Value);
        if (tripFrom.HasValue)
            query = query.Where(claim => claim.CurrentVersion!.MealAllowance!.ReturnDate >= tripFrom.Value);
        if (tripTo.HasValue)
            query = query.Where(claim => claim.CurrentVersion!.MealAllowance!.DepartureDate <= tripTo.Value);
        return query;
    }
}
