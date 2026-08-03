using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace TravelReimbursement.Api.Data;

public sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
            ?? "Host=localhost;Port=5432;Database=travel_reimbursement;Username=travel_app;Password=design-time-only";
        var options = new DbContextOptionsBuilder<AppDbContext>();
        options.UseNpgsql(connectionString);
        return new AppDbContext(options.Options);
    }
}
