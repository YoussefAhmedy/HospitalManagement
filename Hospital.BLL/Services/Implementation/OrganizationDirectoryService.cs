using Hospital.BLL.Services.Abstraction;
using Hospital.DAL.DataBase;
using Microsoft.EntityFrameworkCore;

namespace Hospital.BLL.Services.Implementation;

public sealed class OrganizationDirectoryService(HospitalDbContext dbContext) : IOrganizationDirectoryService
{
    public async Task<OrganizationSnapshot> SnapshotAsync(CancellationToken cancellationToken = default)
    {
        var branches = await dbContext.Branches.AsNoTracking()
            .Where(b => b.IsActive)
            .OrderBy(b => b.Name)
            .Select(b => new LookupOption(b.Id, b.Name, b.Code))
            .ToListAsync(cancellationToken);

        var departments = await dbContext.Departments.AsNoTracking()
            .Where(d => d.IsActive)
            .OrderBy(d => d.Name)
            .Select(d => new LookupOption(d.Id, d.Name, d.Code))
            .ToListAsync(cancellationToken);

        var doctors = await dbContext.Doctors.AsNoTracking()
            .Where(d => d.IsActive)
            .OrderBy(d => d.LastName)
            .Select(d => new LookupOption(
                0,
                "Dr. " + d.FirstName + " " + d.LastName,
                d.Specialization == null ? null : d.Specialization.Name))
            .ToListAsync(cancellationToken);

        var labTests = await dbContext.LabTests.AsNoTracking()
            .Where(t => t.IsActive)
            .OrderBy(t => t.Name)
            .Select(t => new LookupOption(t.Id, t.Name + " (" + t.Code + ")", t.Category))
            .ToListAsync(cancellationToken);

        var medications = await dbContext.Medications.AsNoTracking()
            .Where(m => m.IsActive)
            .OrderBy(m => m.Name)
            .Select(m => new LookupOption(m.Id, m.Name + " " + m.Strength, m.Form))
            .ToListAsync(cancellationToken);

        var serviceItems = await dbContext.ServiceItems.AsNoTracking()
            .Where(s => s.IsActive)
            .OrderBy(s => s.Name)
            .Select(s => new LookupOption(s.Id, s.Name, s.Category.ToString()))
            .ToListAsync(cancellationToken);

        return new OrganizationSnapshot(branches, departments, doctors, labTests, medications, serviceItems);
    }
}
