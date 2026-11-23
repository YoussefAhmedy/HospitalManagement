namespace Hospital.BLL.Common;

/// <summary>Server-side pagination envelope used by every list and reporting surface.</summary>
public sealed class PagedResult<T>
{
    public PagedResult(IReadOnlyList<T> items, int totalCount, int page, int pageSize)
    {
        Items = items;
        TotalCount = totalCount;
        Page = page < 1 ? 1 : page;
        PageSize = pageSize is < 1 or > 200 ? 20 : pageSize;
    }

    public IReadOnlyList<T> Items { get; }
    public int TotalCount { get; }
    public int Page { get; }
    public int PageSize { get; }

    public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool HasPrevious => Page > 1;
    public bool HasNext => Page < TotalPages;

    public static PagedResult<T> Empty(int pageSize = 20) => new([], 0, 1, pageSize);
}

/// <summary>Inclusive date range filter applied in UTC. Null bounds mean "unbounded".</summary>
public sealed record DateRange(DateOnly? From, DateOnly? To)
{
    public DateTime? FromUtc => From?.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
    public DateTime? ToUtcExclusive => To?.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

    public static DateRange LastDays(int days, DateOnly today) =>
        new(today.AddDays(-(days - 1)), today);

    public static DateRange ThisMonth(DateOnly today) =>
        new(new DateOnly(today.Year, today.Month, 1), today);
}
