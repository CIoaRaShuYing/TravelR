using Microsoft.EntityFrameworkCore;
using TravelReimbursement.Api.Data;
using TravelReimbursement.Api.Domain;
using TravelReimbursement.Api.Services;

namespace TravelReimbursement.Api.Tests;

public sealed class CostDashboardQueryTests
{
    [Fact]
    public void Combines_net_salary_reimbursement_and_meal_without_double_counting()
    {
        var user = User();
        var claim = Claim(user, new Project(), 100m, PayoutStatus.Pending, 50m, PayoutStatus.Paid);
        claim.ArchiveBatchId = Guid.NewGuid();
        var salary = Salary(user, 8000m, PayoutStatus.Paid);
        salary.GrossPay = 10000m;
        var query = Query([salary], [claim]);

        var group = Assert.Single(CostDashboardQuery.Group(query, false));
        var totals = CostDashboardQuery.Summarize([group]);

        Assert.Equal(3, group.ItemCount);
        Assert.Equal(8000m, totals.PayrollTotal);
        Assert.Equal(100m, totals.ReimbursementTotal);
        Assert.Equal(50m, totals.MealAllowanceTotal);
        Assert.Equal(8050m, totals.PaidAmount);
        Assert.Equal(100m, totals.PendingAmount);
        Assert.Equal(8150m, totals.TotalAmount);
    }

    [Fact]
    public void Excludes_unapproved_claims_and_unlocked_or_cancelled_payroll()
    {
        var user = User();
        var claims = Enum.GetValues<ClaimStatus>().Select(status =>
        {
            var claim = Claim(user, new Project(), 100m, PayoutStatus.Pending);
            claim.Status = status;
            return claim;
        }).ToArray();
        var payroll = Enum.GetValues<PayrollPeriodStatus>().Select(status =>
        {
            var entry = Salary(user, 1000m, PayoutStatus.Pending);
            entry.PayrollPeriod.Status = status;
            return entry;
        }).ToArray();

        var rows = Query(payroll, claims).ToList();

        Assert.Equal(2, rows.Count(row => row.Category == CostCategory.Payroll));
        Assert.Single(rows.Where(row => row.Category == CostCategory.Reimbursement));
        Assert.DoesNotContain(rows, row => row.Category == CostCategory.MealAllowance);
    }

    [Fact]
    public void Uses_only_current_meal_and_its_own_payout_status()
    {
        var user = User();
        var claim = Claim(user, new Project(), 100m, PayoutStatus.Paid, 60m, PayoutStatus.Pending);
        var oldVersion = new ClaimVersion { Claim = claim, ClaimId = claim.Id };
        oldVersion.MealAllowance = new MealAllowance
        {
            ClaimVersion = oldVersion, ClaimVersionId = oldVersion.Id,
            Status = MealAllowanceStatus.Approved, PayoutStatus = PayoutStatus.Paid, TotalAmount = 999m
        };
        claim.Versions.Add(oldVersion);
        var totals = CostDashboardQuery.Summarize(CostDashboardQuery.Group(Query([], [claim]), false).ToList());

        Assert.Equal(100m, totals.ReimbursementPaid);
        Assert.Equal(60m, totals.MealAllowancePending);
        Assert.Equal(0m, totals.MealAllowancePaid);
        Assert.Equal(160m, totals.TotalAmount);
    }

    [Fact]
    public void Excludes_unapproved_unpriced_and_cancelled_meals()
    {
        var claims = Enum.GetValues<MealAllowanceStatus>().Select(status =>
        {
            var claim = Claim(User(), new Project(), 100m, PayoutStatus.Paid, 50m);
            claim.CurrentVersion!.MealAllowance!.Status = status;
            return claim;
        }).ToList();
        var unpriced = Claim(User(), new Project(), 100m, PayoutStatus.Paid, 50m);
        unpriced.CurrentVersion!.MealAllowance!.TotalAmount = null;
        var cancelled = Claim(User(), new Project(), 100m, PayoutStatus.Paid, 50m);
        cancelled.Status = ClaimStatus.Cancelled;
        claims.AddRange([unpriced, cancelled]);

        Assert.Single(Query([], claims).Where(row => row.Category == CostCategory.MealAllowance));
    }

    [Fact]
    public void Project_scope_excludes_all_payroll_and_keeps_person_and_category_filters()
    {
        var user = User();
        var project = new Project { Name = "项目 A", Code = "A" };
        var claims = new[]
        {
            Claim(user, project, 100m, PayoutStatus.Paid, 50m),
            Claim(user, new Project(), 200m, PayoutStatus.Pending),
            Claim(User(), project, 300m, PayoutStatus.Pending)
        };
        var query = Query([Salary(user, 8000m)], claims);

        var byProject = CostDashboardQuery.Filter(query, null, user.Id, null, true);
        Assert.DoesNotContain(byProject, row => row.Category == CostCategory.Payroll);
        Assert.Equal(2, CostDashboardQuery.Group(byProject, true).Count());
        var filtered = CostDashboardQuery.Filter(query, CostCategory.MealAllowance, user.Id, project.Id, false);
        var meal = Assert.Single(filtered);
        Assert.Equal(50m, meal.Amount);
        var projectRows = CostDashboardQuery.Filter(query, null, null, project.Id, false).ToList();
        Assert.Equal(3, projectRows.Count);
        Assert.DoesNotContain(projectRows, row => row.Category == CostCategory.Payroll);
        Assert.Empty(CostDashboardQuery.Filter(query, CostCategory.Payroll, null, project.Id, false));
    }

    [Fact]
    public void Same_names_remain_separate_and_inactive_users_are_included()
    {
        var inactive = User();
        inactive.IsActive = false;
        var another = User();
        var query = Query([Salary(inactive, 10m), Salary(another, 20m)],
            [Claim(inactive, new Project(), 30m, PayoutStatus.Pending)]);

        var groups = CostDashboardQuery.Group(query, false).ToList();

        Assert.Equal(2, groups.Count);
        Assert.Equal(40m, groups.Single(group => group.Id == inactive.Id).Totals.TotalAmount);
        Assert.Equal(20m, groups.Single(group => group.Id == another.Id).Totals.TotalAmount);
    }

    [Fact]
    public void Totals_cover_all_rows_and_empty_results_are_zero()
    {
        var user = User();
        var payroll = Enumerable.Range(1, 25).Select(_ => Salary(user, 0.01m)).ToArray();
        var query = Query(payroll, []);
        var groups = CostDashboardQuery.Group(query, false).ToList();

        Assert.Equal(25, Assert.Single(groups).ItemCount);
        Assert.Equal(0.25m, CostDashboardQuery.Summarize(groups).TotalAmount);
        Assert.Equal(20, query.Take(20).Count());
        Assert.Equal(0m, CostDashboardQuery.Summarize([]).TotalAmount);
        Assert.Empty(CostDashboardQuery.Filter(query, null, Guid.NewGuid(), null, false));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Npgsql_translates_union_filters_groups_paging_and_options(bool byProject)
    {
        using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=query_only;Username=query_only;Password=query_only").Options);
        var query = CostDashboardQuery.Filter(CostDashboardQuery.Build(db.PayrollEntries, db.ReimbursementClaims),
            CostCategory.Reimbursement, Guid.NewGuid(), byProject ? Guid.NewGuid() : null, byProject);

        Assert.Contains("GROUP BY", CostDashboardQuery.Group(query, byProject).ToQueryString());
        Assert.Contains("LIMIT", query.OrderBy(row => row.UserName).ThenBy(row => row.UserId)
            .ThenBy(row => row.Category).ThenBy(row => row.Id).Skip(20).Take(20).ToQueryString());
        Assert.Contains("DISTINCT", query.Select(row => new { row.UserId, row.UserName, row.UserDisplayName }).Distinct()
            .OrderBy(row => row.UserName).Select(row => new CostDashboardOption(row.UserId, row.UserName, row.UserDisplayName)).ToQueryString());
        Assert.Contains("DISTINCT", query.Where(row => row.ProjectId != null)
            .Select(row => new { row.ProjectId, row.ProjectName, row.ProjectCode }).Distinct().OrderBy(row => row.ProjectCode)
            .Select(row => new CostDashboardOption(row.ProjectId!.Value, row.ProjectName!, row.ProjectCode!)).ToQueryString());
    }

    private static IQueryable<CostDashboardItem> Query(IEnumerable<PayrollEntry> payroll, IEnumerable<ReimbursementClaim> claims) =>
        CostDashboardQuery.Build(payroll.AsQueryable(), claims.AsQueryable());

    private static AppUser User() => new() { Id = Guid.NewGuid(), DisplayName = "同名人员", PersonalName = "张三", IsActive = true };

    private static PayrollEntry Salary(AppUser user, decimal amount, PayoutStatus status = PayoutStatus.Pending)
    {
        var period = new PayrollPeriod { Status = PayrollPeriodStatus.ReadyForPayout, PayrollMonth = new DateOnly(2026, 9, 1) };
        return new PayrollEntry { User = user, UserId = user.Id, PayrollPeriod = period, PayrollPeriodId = period.Id, NetPay = amount, PayoutStatus = status };
    }

    private static ReimbursementClaim Claim(AppUser user, Project project, decimal amount, PayoutStatus payout,
        decimal? mealAmount = null, PayoutStatus mealPayout = PayoutStatus.Pending)
    {
        var claim = new ReimbursementClaim { Applicant = user, ApplicantId = user.Id, Status = ClaimStatus.Approved, PayoutStatus = payout };
        var version = new ClaimVersion { Claim = claim, ClaimId = claim.Id, Project = project, ProjectId = project.Id, TotalAmount = amount };
        claim.CurrentVersion = version;
        claim.CurrentVersionId = version.Id;
        claim.Versions.Add(version);
        if (mealAmount.HasValue) version.MealAllowance = new MealAllowance
        {
            ClaimVersion = version, ClaimVersionId = version.Id, Status = MealAllowanceStatus.Approved,
            PayoutStatus = mealPayout, TotalAmount = mealAmount
        };
        return claim;
    }
}
