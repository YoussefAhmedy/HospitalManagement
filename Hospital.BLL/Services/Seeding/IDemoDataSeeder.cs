namespace Hospital.BLL.Services.Seeding;

/// <summary>Creates synthetic, clearly labelled demonstration records. Never real patient data.</summary>
public interface IDemoDataSeeder
{
    Task<string> SeedAsync(CancellationToken cancellationToken = default);
}
