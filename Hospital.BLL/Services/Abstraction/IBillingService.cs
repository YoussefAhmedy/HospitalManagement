using Hospital.BLL.Common;
using Hospital.DAL.Entities.Billing;

namespace Hospital.BLL.Services.Abstraction;

public sealed record InvoiceListItem(
    int Id, string Number, string Mrn, string PatientName, DateTime IssuedAtUtc,
    InvoiceStatus Status, decimal Total, decimal InsuranceCovered, decimal Paid, decimal Balance);

public sealed record RevenueSummary(
    decimal RevenueToday,
    decimal RevenueThisMonth,
    decimal Outstanding,
    int OpenInvoices,
    decimal AverageInvoiceValue);

public sealed record AddInvoiceLine(string Description, int Quantity, decimal UnitPrice, int? ServiceItemId = null);

public interface IBillingService
{
    Task<Result<Invoice>> CreateInvoiceAsync(
        string patientId, int? encounterId, IReadOnlyCollection<AddInvoiceLine> lines,
        string actorUserId, CancellationToken cancellationToken = default);

    Task<Result<Payment>> RecordPaymentAsync(
        int invoiceId, decimal amount, PaymentMethod method, string? reference,
        string actorUserId, CancellationToken cancellationToken = default);

    Task<Result<Payment>> RecordRefundAsync(
        int invoiceId, decimal amount, string reason, string actorUserId,
        CancellationToken cancellationToken = default);

    Task<Result> ApproveRefundAsync(int paymentId, string approverUserId, CancellationToken cancellationToken = default);

    Task<Invoice?> GetAsync(int invoiceId, CancellationToken cancellationToken = default);

    Task<PagedResult<InvoiceListItem>> QueryAsync(
        InvoiceStatus? status, DateRange range, string? term, int page, int pageSize,
        CancellationToken cancellationToken = default);

    Task<RevenueSummary> RevenueAsync(CancellationToken cancellationToken = default);
}
