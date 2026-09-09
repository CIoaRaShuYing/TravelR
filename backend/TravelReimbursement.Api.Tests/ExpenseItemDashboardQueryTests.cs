using TravelReimbursement.Api.Domain;
using TravelReimbursement.Api.Services;

namespace TravelReimbursement.Api.Tests;

public sealed class ExpenseItemDashboardQueryTests
{
    [Fact]
    public void Apply_returns_only_matching_current_version_expense_items()
    {
        var projectId = Guid.NewGuid();
        var applicantId = Guid.NewGuid();
        var matching = Expense(ClaimStatus.Approved, ExpenseCategory.Lodging, projectId, applicantId, new DateOnly(2026, 9, 8));
        var oldVersion = Expense(ClaimStatus.Approved, ExpenseCategory.Lodging, projectId, applicantId, new DateOnly(2026, 9, 8), false);
        var otherCategory = Expense(ClaimStatus.Approved, ExpenseCategory.Meal, projectId, applicantId, new DateOnly(2026, 9, 8));
        var otherProject = Expense(ClaimStatus.Approved, ExpenseCategory.Lodging, Guid.NewGuid(), applicantId, new DateOnly(2026, 9, 8));
        var otherApplicant = Expense(ClaimStatus.Approved, ExpenseCategory.Lodging, projectId, Guid.NewGuid(), new DateOnly(2026, 9, 8));
        var outsideDate = Expense(ClaimStatus.Approved, ExpenseCategory.Lodging, projectId, applicantId, new DateOnly(2026, 8, 31));

        var result = ExpenseItemDashboardQuery.Apply(
                new[] { matching, oldVersion, otherCategory, otherProject, otherApplicant, outsideDate }.AsQueryable(),
                ExpenseCategory.Lodging,
                projectId,
                applicantId,
                new DateOnly(2026, 9, 8),
                new DateOnly(2026, 9, 8))
            .ToList();

        Assert.Single(result);
        Assert.Same(matching, result[0]);
    }

    [Fact]
    public void Apply_excludes_draft_cancelled_and_unspecified_items()
    {
        var projectId = Guid.NewGuid();
        var applicantId = Guid.NewGuid();
        var submitted = Expense(ClaimStatus.Submitted, ExpenseCategory.Other, projectId, applicantId, new DateOnly(2026, 9, 1));
        var approved = Expense(ClaimStatus.Approved, ExpenseCategory.Other, projectId, applicantId, new DateOnly(2026, 9, 2));
        var rejected = Expense(ClaimStatus.Rejected, ExpenseCategory.Other, projectId, applicantId, new DateOnly(2026, 9, 3));
        var draft = Expense(ClaimStatus.Draft, ExpenseCategory.Other, projectId, applicantId, new DateOnly(2026, 9, 4));
        var cancelled = Expense(ClaimStatus.Cancelled, ExpenseCategory.Other, projectId, applicantId, new DateOnly(2026, 9, 5));
        var unspecified = Expense(ClaimStatus.Approved, ExpenseCategory.Unspecified, projectId, applicantId, new DateOnly(2026, 9, 6));

        var result = ExpenseItemDashboardQuery.Apply(
                new[] { submitted, approved, rejected, draft, cancelled, unspecified }.AsQueryable(),
                null,
                null,
                null,
                null,
                null)
            .ToList();

        Assert.Equal(new[] { submitted, approved, rejected }, result);
    }

    [Fact]
    public void Apply_filters_expenses_by_archive_state()
    {
        var archived = Expense(ClaimStatus.Approved, ExpenseCategory.Other, Guid.NewGuid(), Guid.NewGuid(), new DateOnly(2026, 9, 1));
        archived.ClaimVersion.Claim.ArchiveBatchId = Guid.NewGuid();
        var unarchived = Expense(ClaimStatus.Approved, ExpenseCategory.Other, Guid.NewGuid(), Guid.NewGuid(), new DateOnly(2026, 9, 1));

        var result = ExpenseItemDashboardQuery.Apply(
            new[] { archived, unarchived }.AsQueryable(),
            null, null, null, null, null, "unarchived").ToList();

        Assert.Equal(new[] { unarchived }, result);
    }

    private static ExpenseItem Expense(
        ClaimStatus status,
        ExpenseCategory category,
        Guid projectId,
        Guid applicantId,
        DateOnly? expenseDate,
        bool currentVersion = true)
    {
        var claim = new ReimbursementClaim { ApplicantId = applicantId, Status = status };
        var version = new ClaimVersion { Claim = claim, ClaimId = claim.Id, ProjectId = projectId };
        claim.CurrentVersionId = currentVersion ? version.Id : Guid.NewGuid();
        claim.CurrentVersion = currentVersion ? version : new ClaimVersion { Id = claim.CurrentVersionId.Value, Claim = claim, ClaimId = claim.Id, ProjectId = projectId };
        var item = new ExpenseItem
        {
            ClaimVersion = version,
            ClaimVersionId = version.Id,
            Category = category,
            ExpenseDate = expenseDate
        };
        version.ExpenseItems.Add(item);
        return item;
    }
}
