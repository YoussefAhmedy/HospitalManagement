using Hospital.DAL.Entities.Diagnostics;

namespace Hospital.BLL.Common.Rules;

public static class LabRules
{
    /// <summary>
    /// Derives the abnormal flag strictly from the configured reference range. It never
    /// invents a clinical interpretation; a critical value is only a flag that must be
    /// verified by a laboratory professional.
    /// </summary>
    public static ResultFlag Classify(
        decimal? value,
        decimal? normalLow,
        decimal? normalHigh,
        decimal? criticalLow,
        decimal? criticalHigh)
    {
        if (value is null)
        {
            return ResultFlag.Normal;
        }

        if ((criticalLow is not null && value < criticalLow) ||
            (criticalHigh is not null && value > criticalHigh))
        {
            return ResultFlag.Critical;
        }

        if (normalLow is not null && value < normalLow)
        {
            return ResultFlag.Low;
        }

        if (normalHigh is not null && value > normalHigh)
        {
            return ResultFlag.High;
        }

        return ResultFlag.Normal;
    }

    public static readonly IReadOnlyDictionary<LabOrderStatus, LabOrderStatus[]> Transitions =
        new Dictionary<LabOrderStatus, LabOrderStatus[]>
        {
            [LabOrderStatus.Requested] = [LabOrderStatus.AwaitingCollection, LabOrderStatus.Cancelled],
            [LabOrderStatus.AwaitingCollection] = [LabOrderStatus.Collected, LabOrderStatus.Cancelled],
            [LabOrderStatus.Collected] = [LabOrderStatus.Received, LabOrderStatus.Cancelled],
            [LabOrderStatus.Received] = [LabOrderStatus.Processing, LabOrderStatus.Cancelled],
            [LabOrderStatus.Processing] = [LabOrderStatus.AwaitingVerification, LabOrderStatus.Cancelled],
            [LabOrderStatus.AwaitingVerification] = [LabOrderStatus.Released, LabOrderStatus.Processing],
            [LabOrderStatus.Released] = [],
            [LabOrderStatus.Cancelled] = []
        };

    public static bool CanTransition(LabOrderStatus from, LabOrderStatus to) =>
        Transitions.TryGetValue(from, out var allowed) && allowed.Contains(to);

    /// <summary>A result may only be shown to a clinician as final once released.</summary>
    public static bool IsClinicallyFinal(LabOrderStatus status) => status == LabOrderStatus.Released;

    /// <summary>Entry is allowed only while the order is in the laboratory's hands.</summary>
    public static bool CanEnterResults(LabOrderStatus status) =>
        status is LabOrderStatus.Received or LabOrderStatus.Processing or LabOrderStatus.AwaitingVerification;

    /// <summary>Verification requires every item to have a value.</summary>
    public static bool CanVerify(LabOrderStatus status, IEnumerable<LabOrderItem> items) =>
        status == LabOrderStatus.Processing
        && items.Any()
        && items.All(item => !string.IsNullOrWhiteSpace(item.ResultValue) || item.NumericResult is not null);
}
