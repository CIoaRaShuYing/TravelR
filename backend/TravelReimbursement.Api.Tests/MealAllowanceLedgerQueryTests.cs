using TravelReimbursement.Api.Domain;
using TravelReimbursement.Api.Services;

namespace TravelReimbursement.Api.Tests;

public sealed class MealAllowanceLedgerQueryTests
{
    [Fact]
    public void Apply_filters_current_version_by_project_applicant_and_overlapping_trip_dates()
    {
        var matchingProjectId = Guid.NewGuid();
        var matchingApplicantId = Guid.NewGuid();
        var matchingClaim = Claim(matchingProjectId, matchingApplicantId, new DateOnly(2026, 9, 5), new DateOnly(2026, 9, 10));
        var outsideTrip = Claim(matchingProjectId, matchingApplicantId, new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 3));
        var otherProject = Claim(Guid.NewGuid(), matchingApplicantId, new DateOnly(2026, 9, 6), new DateOnly(2026, 9, 7));
        var otherApplicant = Claim(matchingProjectId, Guid.NewGuid(), new DateOnly(2026, 9, 6), new DateOnly(2026, 9, 7));
        var incompleteTrip = Claim(matchingProjectId, matchingApplicantId, null, new DateOnly(2026, 9, 10));
        var withoutMealAllowance = Claim(matchingProjectId, matchingApplicantId, null, null, false);

        var result = MealAllowanceLedgerQuery.Apply(
                new[] { matchingClaim, outsideTrip, otherProject, otherApplicant, incompleteTrip, withoutMealAllowance }.AsQueryable(),
                matchingProjectId,
                matchingApplicantId,
                new DateOnly(2026, 9, 8),
                new DateOnly(2026, 9, 12))
            .ToList();

        Assert.Single(result);
        Assert.Same(matchingClaim, result[0]);
    }

    [Fact]
    public void Apply_does_not_return_meal_allowance_from_superseded_version()
    {
        var claim = Claim(Guid.NewGuid(), Guid.NewGuid(), null, null, false);
        claim.Versions.Add(new ClaimVersion
        {
            ClaimId = claim.Id,
            MealAllowance = new MealAllowance
            {
                DepartureDate = new DateOnly(2026, 9, 1),
                ReturnDate = new DateOnly(2026, 9, 2)
            }
        });

        var result = MealAllowanceLedgerQuery.Apply(new[] { claim }.AsQueryable(), null, null, null, null);

        Assert.Empty(result);
    }

    private static ReimbursementClaim Claim(Guid projectId, Guid applicantId, DateOnly? departureDate, DateOnly? returnDate, bool withMealAllowance = true)
    {
        var claim = new ReimbursementClaim { ApplicantId = applicantId };
        var version = new ClaimVersion
        {
            ClaimId = claim.Id,
            ProjectId = projectId,
            MealAllowance = withMealAllowance
                ? new MealAllowance { DepartureDate = departureDate, ReturnDate = returnDate }
                : null
        };
        claim.CurrentVersion = version;
        claim.CurrentVersionId = version.Id;
        claim.Versions.Add(version);
        return claim;
    }
}
