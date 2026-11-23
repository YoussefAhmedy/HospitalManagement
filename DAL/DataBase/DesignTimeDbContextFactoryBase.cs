using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Hospital.DAL.DataBase;

/// <summary>
/// Shared design-time factory. EF tooling (and CI migration jobs) can create the context
/// without triggering application startup side effects such as role seeding, Hangfire
/// registration, or network calls.
/// </summary>
public abstract class DesignTimeDbContextFactoryBase<TContext> : IDesignTimeDbContextFactory<TContext>
    where TContext : DbContext
{
    protected abstract TContext Create(DbContextOptions<TContext> options);

    public TContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
            ?? "Server=(localdb)\\MSSQLLocalDB;Database=CareAxisDesignTime;Trusted_Connection=True;TrustServerCertificate=True";

        var options = new DbContextOptionsBuilder<TContext>()
            .UseSqlServer(connectionString)
            .Options;
        return Create(options);
    }
}
