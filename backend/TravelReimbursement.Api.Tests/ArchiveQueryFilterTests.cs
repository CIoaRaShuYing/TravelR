using TravelReimbursement.Api.Domain;
using TravelReimbursement.Api.Services;

namespace TravelReimbursement.Api.Tests;

public sealed class ArchiveQueryFilterTests
{
    [Theory]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData("all", true)]
    [InlineData("ARCHIVED", true)]
    [InlineData("unarchived", true)]
    [InlineData("unknown", false)]
    public void IsValidState_accepts_only_supported_values(string? state, bool expected)
    {
        Assert.Equal(expected, ArchiveQueryFilter.IsValidState(state));
    }

    [Fact]
    public void Exact_batch_takes_precedence_over_archive_state()
    {
        var batchId = Guid.NewGuid();
        var matching = new ReimbursementClaim { ArchiveBatchId = batchId };
        var other = new ReimbursementClaim { ArchiveBatchId = Guid.NewGuid() };
        var unarchived = new ReimbursementClaim();

        var result = ArchiveQueryFilter.Apply(
            new[] { matching, other, unarchived }.AsQueryable(),
            "unarchived",
            batchId).ToList();

        Assert.Equal(new[] { matching }, result);
    }
}
