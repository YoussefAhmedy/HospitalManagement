using Hospital.DAL.DataBase;
using Hospital.BLL.Services.Seeding;
using Microsoft.EntityFrameworkCore;

namespace HospitalManagement.Infrastructure;

/// <summary>
/// Applies pending migrations and optional synthetic demo data at start-up, only in the
/// explicit Development/Performance environments. Production never seeds and never
/// migrates automatically.
/// </summary>
public static class DevelopmentSeederRunner
{
    public static async Task RunAsync(IServiceProvider services, IConfiguration configuration, IHostEnvironment environment, ILogger logger)
    {
        if (environment.IsProduction())
        {
            return;
        }

        using var scope = services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<HospitalDbContext>();

        if (configuration.GetValue("Database:AutoMigrate", true))
        {
            await DatabaseSchemaInitializer.InitialiseAsync(dbContext, environment, configuration, logger);
        }

        if (environment.EnvironmentName is "Performance" ||
            configuration.GetValue("Demo:Seed", environment.IsDevelopment()))
        {
            var seeder = scope.ServiceProvider.GetRequiredService<IDemoDataSeeder>();
            var message = await seeder.SeedAsync();
            logger.LogWarning("{DemoMessage}", message);
        }
    }
}
