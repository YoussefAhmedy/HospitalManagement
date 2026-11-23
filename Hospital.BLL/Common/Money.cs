namespace Hospital.BLL.Common;

/// <summary>Currency formatting kept in one place so reports, invoices and receipts agree.</summary>
public static class Money
{
    public const string CultureCode = "en-US";
    public const string CurrencyCode = "USD";

    public static string Format(decimal amount) =>
        amount.ToString("C2", System.Globalization.CultureInfo.GetCultureInfo(CultureCode));

    public static string Plain(decimal amount) =>
        amount.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
}
