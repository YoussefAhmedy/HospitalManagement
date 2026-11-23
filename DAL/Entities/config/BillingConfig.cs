using Hospital.DAL.Entities.Billing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hospital.DAL.Entities.config;

internal sealed class ServiceItemConfig : IEntityTypeConfiguration<ServiceItem>
{
    public void Configure(EntityTypeBuilder<ServiceItem> builder)
    {
        builder.ToTable("ServiceItems");
        builder.HasIndex(s => s.Code).IsUnique();
        builder.Property(s => s.Price).HasPrecision(18, 2);
        builder.HasOne(s => s.Department).WithMany().HasForeignKey(s => s.DepartmentId).OnDelete(DeleteBehavior.SetNull);
    }
}

internal sealed class InvoiceConfig : IEntityTypeConfiguration<Invoice>
{
    public void Configure(EntityTypeBuilder<Invoice> builder)
    {
        builder.ToTable("Invoices");
        builder.HasIndex(i => i.Number).IsUnique();
        builder.HasIndex(i => new { i.Status, i.IssuedAtUtc });
        builder.HasIndex(i => new { i.PatientId, i.IssuedAtUtc });
        builder.HasOne(i => i.Encounter).WithMany().HasForeignKey(i => i.EncounterId).OnDelete(DeleteBehavior.SetNull);
        builder.HasMany(i => i.Lines).WithOne(l => l.Invoice!).HasForeignKey(l => l.InvoiceId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(i => i.Payments).WithOne(p => p.Invoice!).HasForeignKey(p => p.InvoiceId).OnDelete(DeleteBehavior.Cascade);
        // Computed totals are projection-only and never persisted.
        builder.Ignore(i => i.Total);
        builder.Ignore(i => i.Paid);
        builder.Ignore(i => i.Balance);
    }
}

internal sealed class InvoiceLineConfig : IEntityTypeConfiguration<InvoiceLine>
{
    public void Configure(EntityTypeBuilder<InvoiceLine> builder)
    {
        builder.ToTable("InvoiceLines");
        builder.Property(l => l.UnitPrice).HasPrecision(18, 2);
        builder.Ignore(l => l.Amount);
        builder.HasOne(l => l.ServiceItem).WithMany().HasForeignKey(l => l.ServiceItemId).OnDelete(DeleteBehavior.SetNull);
    }
}

internal sealed class PaymentConfig : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.ToTable("Payments");
        builder.HasIndex(p => p.ReceiptNumber).IsUnique();
        builder.HasIndex(p => p.PaidAtUtc);
        builder.Property(p => p.Amount).HasPrecision(18, 2);
    }
}

internal sealed class InsurancePolicyConfig : IEntityTypeConfiguration<InsurancePolicy>
{
    public void Configure(EntityTypeBuilder<InsurancePolicy> builder)
    {
        builder.ToTable("InsurancePolicies");
        builder.Property(p => p.CoveragePercent).HasPrecision(5, 2);
        builder.HasIndex(p => new { p.PatientId, p.IsActive });
    }
}
