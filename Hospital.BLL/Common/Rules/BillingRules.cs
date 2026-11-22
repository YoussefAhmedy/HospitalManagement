using Hospital.DAL.Entities.Billing;

namespace Hospital.BLL.Common.Rules;

public static class BillingRules
{
    public static decimal Balance(decimal total, decimal insuranceCovered, decimal paid) =>
        Math.Round(total - insuranceCovered - paid, 2);

    /// <summary>Overpayment is rejected so the ledger can never go negative.</summary>
    public static bool IsPaymentValid(decimal amount, decimal balance) =>
        amount > 0 && amount <= Math.Round(balance, 2);

    /// <summary>Refunds at or above the threshold require a second authorised approval.</summary>
    public static bool RefundRequiresApproval(decimal amount, decimal threshold = 100m) =>
        amount >= threshold;

    public static InvoiceStatus DeriveStatus(decimal total, decimal insuranceCovered, decimal paid, bool isVoid) =>
        isVoid ? InvoiceStatus.Void
        : paid <= 0 && insuranceCovered <= 0 ? InvoiceStatus.Issued
        : Balance(total, insuranceCovered, paid) <= 0 ? InvoiceStatus.Paid
        : InvoiceStatus.PartiallyPaid;

    /// <summary>Insurance coverage is capped at the invoice total and the policy percentage.</summary>
    public static decimal InsuranceCoverage(decimal total, decimal coveragePercent) =>
        Math.Round(total * Math.Clamp(coveragePercent, 0, 100) / 100m, 2);
}
