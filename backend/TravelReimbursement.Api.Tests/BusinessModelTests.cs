using Microsoft.EntityFrameworkCore;
using TravelReimbursement.Api.Data;
using TravelReimbursement.Api.Domain;

namespace TravelReimbursement.Api.Tests;

public sealed class BusinessModelTests
{
    [Fact]
    public void Weekly_report_is_unique_per_author_project_and_week()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=model_only;Username=model_only;Password=model_only")
            .Options;
        using var db = new AppDbContext(options);

        var entity = db.Model.FindEntityType(typeof(WeeklyReport));
        var uniqueIndex = entity!.GetIndexes().Single(index => index.IsUnique);

        Assert.Equal(new[] { nameof(WeeklyReport.AuthorId), nameof(WeeklyReport.ProjectId), nameof(WeeklyReport.WeekStart) }, uniqueIndex.Properties.Select(property => property.Name));
    }
}
