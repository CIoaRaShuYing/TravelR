using TravelReimbursement.Api.Domain;

namespace TravelReimbursement.Api.Services;

public static class ArchiveQueryFilter
{
    public static bool IsValidState(string? archiveState) => string.IsNullOrWhiteSpace(archiveState)
        || archiveState.Equals("all", StringComparison.OrdinalIgnoreCase)
        || archiveState.Equals("archived", StringComparison.OrdinalIgnoreCase)
        || archiveState.Equals("unarchived", StringComparison.OrdinalIgnoreCase);

    public static IQueryable<ReimbursementClaim> Apply(
        IQueryable<ReimbursementClaim> query,
        string? archiveState,
        Guid? archiveBatchId)
    {
        if (archiveBatchId.HasValue)
            return query.Where(claim => claim.ArchiveBatchId == archiveBatchId.Value);
        if (archiveState?.Equals("archived", StringComparison.OrdinalIgnoreCase) == true)
            return query.Where(claim => claim.ArchiveBatchId != null);
        if (archiveState?.Equals("unarchived", StringComparison.OrdinalIgnoreCase) == true)
            return query.Where(claim => claim.ArchiveBatchId == null);
        return query;
    }
}
