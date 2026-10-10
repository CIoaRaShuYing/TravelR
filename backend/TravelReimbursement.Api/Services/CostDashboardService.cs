using Microsoft.EntityFrameworkCore;
using TravelReimbursement.Api.Data;
using TravelReimbursement.Api.Domain;

namespace TravelReimbursement.Api.Services;

public enum CostCategory { Payroll, Reimbursement, MealAllowance }

public sealed class CostDashboardItem
{
    public Guid Id { get; init; }
    public CostCategory Category { get; init; }
    public Guid UserId { get; init; }
    public string UserName { get; init; } = string.Empty;
    public string UserDisplayName { get; init; } = string.Empty;
    public Guid? ProjectId { get; init; }
    public string? ProjectName { get; init; }
    public string? ProjectCode { get; init; }
    public Guid SourceId { get; init; }
    public string? ClaimNumber { get; init; }
    public DateOnly? PayrollMonth { get; init; }
    public PayoutStatus PayoutStatus { get; init; }
    public decimal Amount { get; init; }
}

public sealed record CostTotals(
    decimal PayrollPaid, decimal PayrollPending,
    decimal ReimbursementPaid, decimal ReimbursementPending,
    decimal MealAllowancePaid, decimal MealAllowancePending)
{
    public decimal PayrollTotal => PayrollPaid + PayrollPending;
    public decimal ReimbursementTotal => ReimbursementPaid + ReimbursementPending;
    public decimal MealAllowanceTotal => MealAllowancePaid + MealAllowancePending;
    public decimal PaidAmount => PayrollPaid + ReimbursementPaid + MealAllowancePaid;
    public decimal PendingAmount => PayrollPending + ReimbursementPending + MealAllowancePending;
    public decimal TotalAmount => PaidAmount + PendingAmount;
}

public sealed record CostDashboardGroup(Guid Id, string Name, string Description, int ItemCount, CostTotals Totals);
public sealed record CostDashboardOption(Guid Id, string Name, string Description);
public sealed record CostDashboardOptions(IReadOnlyList<CostDashboardOption> Users, IReadOnlyList<CostDashboardOption> Projects);
public sealed record CostDashboardResult(
    CostTotals Summary, IReadOnlyList<CostDashboardGroup> Groups, IReadOnlyList<CostDashboardItem> Items,
    int Total, int Page, int PageSize, bool PayrollExcluded);

internal static class CostDashboardQuery
{
    public static IQueryable<CostDashboardItem> Build(IQueryable<PayrollEntry> payroll, IQueryable<ReimbursementClaim> claims)
    {
        var salaries = payroll
            .Where(entry => (entry.PayrollPeriod.Status == PayrollPeriodStatus.ReadyForPayout
                    || entry.PayrollPeriod.Status == PayrollPeriodStatus.Completed)
                && (entry.PayoutStatus == PayoutStatus.Pending || entry.PayoutStatus == PayoutStatus.Paid))
            .Select(entry => new CostDashboardItem
            {
                Id = entry.Id,
                Category = CostCategory.Payroll,
                UserId = entry.UserId,
                UserName = entry.User.PersonalName != null && entry.User.PersonalName != "" ? entry.User.PersonalName : entry.User.DisplayName,
                UserDisplayName = entry.User.DisplayName,
                ProjectId = null,
                ProjectName = null,
                ProjectCode = null,
                SourceId = entry.PayrollPeriodId,
                ClaimNumber = null,
                PayrollMonth = entry.PayrollPeriod.PayrollMonth,
                PayoutStatus = entry.PayoutStatus,
                Amount = entry.NetPay
            });
        var reimbursements = claims
            .Where(claim => claim.CurrentVersion != null && claim.Status == ClaimStatus.Approved
                && (claim.PayoutStatus == PayoutStatus.Pending || claim.PayoutStatus == PayoutStatus.Paid))
            .Select(claim => new CostDashboardItem
            {
                Id = claim.Id,
                Category = CostCategory.Reimbursement,
                UserId = claim.ApplicantId,
                UserName = claim.Applicant.PersonalName != null && claim.Applicant.PersonalName != "" ? claim.Applicant.PersonalName : claim.Applicant.DisplayName,
                UserDisplayName = claim.Applicant.DisplayName,
                ProjectId = claim.CurrentVersion!.ProjectId,
                ProjectName = claim.CurrentVersion.Project.Name,
                ProjectCode = claim.CurrentVersion.Project.Code,
                SourceId = claim.Id,
                ClaimNumber = claim.ClaimNumber,
                PayrollMonth = null,
                PayoutStatus = claim.PayoutStatus,
                Amount = claim.CurrentVersion.TotalAmount
            });
        var meals = claims
            .Where(claim => claim.Status == ClaimStatus.Approved && claim.CurrentVersion != null
                && claim.CurrentVersion.MealAllowance != null
                && claim.CurrentVersion.MealAllowance.Status == MealAllowanceStatus.Approved
                && claim.CurrentVersion.MealAllowance.TotalAmount != null
                && (claim.CurrentVersion.MealAllowance.PayoutStatus == PayoutStatus.Pending
                    || claim.CurrentVersion.MealAllowance.PayoutStatus == PayoutStatus.Paid))
            .Select(claim => new CostDashboardItem
            {
                Id = claim.CurrentVersion!.MealAllowance!.Id,
                Category = CostCategory.MealAllowance,
                UserId = claim.ApplicantId,
                UserName = claim.Applicant.PersonalName != null && claim.Applicant.PersonalName != "" ? claim.Applicant.PersonalName : claim.Applicant.DisplayName,
                UserDisplayName = claim.Applicant.DisplayName,
                ProjectId = claim.CurrentVersion.ProjectId,
                ProjectName = claim.CurrentVersion.Project.Name,
                ProjectCode = claim.CurrentVersion.Project.Code,
                SourceId = claim.Id,
                ClaimNumber = claim.ClaimNumber,
                PayrollMonth = null,
                PayoutStatus = claim.CurrentVersion.MealAllowance.PayoutStatus,
                Amount = claim.CurrentVersion.MealAllowance.TotalAmount!.Value
            });
        return salaries.Concat(reimbursements).Concat(meals);
    }

    public static IQueryable<CostDashboardItem> Filter(
        IQueryable<CostDashboardItem> query, CostCategory? category, Guid? userId, Guid? projectId, bool byProject)
    {
        if (byProject || projectId.HasValue) query = query.Where(item => item.ProjectId != null);
        if (category.HasValue) query = query.Where(item => item.Category == category.Value);
        if (userId.HasValue) query = query.Where(item => item.UserId == userId.Value);
        if (projectId.HasValue) query = query.Where(item => item.ProjectId == projectId.Value);
        return query;
    }

    public static IQueryable<CostDashboardGroup> Group(IQueryable<CostDashboardItem> query, bool byProject) => query
        .GroupBy(item => new
        {
            Id = byProject ? item.ProjectId!.Value : item.UserId,
            Name = byProject ? item.ProjectName! : item.UserName,
            Description = byProject ? item.ProjectCode! : item.UserDisplayName
        })
        .Select(group => new CostDashboardGroup(group.Key.Id, group.Key.Name, group.Key.Description, group.Count(),
            new CostTotals(
                group.Sum(item => item.Category == CostCategory.Payroll && item.PayoutStatus == PayoutStatus.Paid ? item.Amount : 0m),
                group.Sum(item => item.Category == CostCategory.Payroll && item.PayoutStatus == PayoutStatus.Pending ? item.Amount : 0m),
                group.Sum(item => item.Category == CostCategory.Reimbursement && item.PayoutStatus == PayoutStatus.Paid ? item.Amount : 0m),
                group.Sum(item => item.Category == CostCategory.Reimbursement && item.PayoutStatus == PayoutStatus.Pending ? item.Amount : 0m),
                group.Sum(item => item.Category == CostCategory.MealAllowance && item.PayoutStatus == PayoutStatus.Paid ? item.Amount : 0m),
                group.Sum(item => item.Category == CostCategory.MealAllowance && item.PayoutStatus == PayoutStatus.Pending ? item.Amount : 0m))));

    public static CostTotals Summarize(IReadOnlyList<CostDashboardGroup> groups) => new(
        groups.Sum(group => group.Totals.PayrollPaid), groups.Sum(group => group.Totals.PayrollPending),
        groups.Sum(group => group.Totals.ReimbursementPaid), groups.Sum(group => group.Totals.ReimbursementPending),
        groups.Sum(group => group.Totals.MealAllowancePaid), groups.Sum(group => group.Totals.MealAllowancePending));
}

public sealed class CostDashboardService(AppDbContext db)
{
    private IQueryable<CostDashboardItem> Query() => CostDashboardQuery.Build(
        db.PayrollEntries.AsNoTracking(), db.ReimbursementClaims.AsNoTracking());

    public async Task<CostDashboardResult> GetAsync(
        CostCategory? category, Guid? userId, Guid? projectId, string? groupBy, int? page, int? pageSize,
        CancellationToken cancellationToken)
    {
        if (category.HasValue && !Enum.IsDefined(category.Value))
            throw Validation("category", "费用类别必须是 Payroll、Reimbursement 或 MealAllowance。");
        if (groupBy is not null && groupBy != "person" && groupBy != "project")
            throw Validation("groupBy", "划分方式必须是 person 或 project。");
        var byProject = groupBy == "project";
        var query = CostDashboardQuery.Filter(Query(), category, userId, projectId, byProject);
        var groups = await CostDashboardQuery.Group(query, byProject).ToListAsync(cancellationToken);
        groups = groups.OrderByDescending(group => group.Totals.TotalAmount).ThenBy(group => group.Id).ToList();
        var total = groups.Sum(group => group.ItemCount);
        var size = Math.Clamp(pageSize ?? 20, 1, 100);
        var currentPage = Math.Clamp(page ?? 1, 1, Math.Max(1, (int)Math.Ceiling(total / (double)size)));
        var items = await query.OrderBy(item => item.UserName).ThenBy(item => item.UserId)
            .ThenBy(item => item.Category).ThenBy(item => item.Id)
            .Skip((currentPage - 1) * size).Take(size).ToListAsync(cancellationToken);
        return new CostDashboardResult(CostDashboardQuery.Summarize(groups), groups, items, total, currentPage, size,
            byProject || projectId.HasValue);
    }

    public async Task<CostDashboardOptions> GetOptionsAsync(CancellationToken cancellationToken)
    {
        var query = Query();
        var users = await query.Select(item => new { item.UserId, item.UserName, item.UserDisplayName }).Distinct()
            .OrderBy(item => item.UserName).ThenBy(item => item.UserId)
            .Select(item => new CostDashboardOption(item.UserId, item.UserName, item.UserDisplayName)).ToListAsync(cancellationToken);
        var projects = await query.Where(item => item.ProjectId != null)
            .Select(item => new { item.ProjectId, item.ProjectName, item.ProjectCode }).Distinct()
            .OrderBy(item => item.ProjectCode).ThenBy(item => item.ProjectId)
            .Select(item => new CostDashboardOption(item.ProjectId!.Value, item.ProjectName!, item.ProjectCode!)).ToListAsync(cancellationToken);
        return new CostDashboardOptions(users, projects);
    }

    private static ApiProblemException Validation(string field, string message) => new(
        StatusCodes.Status400BadRequest, "VALIDATION_FAILED", message, new() { [field] = [message] });
}
