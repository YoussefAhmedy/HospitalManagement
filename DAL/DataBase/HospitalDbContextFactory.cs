using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Hospital.DAL.DataBase;

/// <summary>
/// Keeps EF tooling independent of application startup side effects such as role seeding
/// and background-job registration. Supply ConnectionStrings__DefaultConnection locally.
/// </summary>
public sealed class HospitalDbContextFactory : IDesignTimeDbContextFactory<HospitalDbContext>
{
    public HospitalDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Set ConnectionStrings__DefaultConnection to a local development database before running EF tooling.");
        }

        var options = new DbContextOptionsBuilder<HospitalDbContext>()
            .UseSqlServer(connectionString)
            .Options;
        return new HospitalDbContext(options);
    }
}
