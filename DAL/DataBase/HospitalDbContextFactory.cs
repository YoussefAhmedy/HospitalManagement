using Microsoft.EntityFrameworkCore;

namespace Hospital.DAL.DataBase;

/// <summary>
/// Keeps EF tooling independent of application startup side effects such as role seeding
/// and background-job registration. Supply ConnectionStrings__DefaultConnection locally;
/// when it is absent a localdb placeholder is used because migration scaffolding only
/// requires the provider, not a reachable server.
/// </summary>
public sealed class HospitalDbContextFactory : DesignTimeDbContextFactoryBase<HospitalDbContext>
{
    protected override HospitalDbContext Create(DbContextOptions<HospitalDbContext> options) => new(options);
}
