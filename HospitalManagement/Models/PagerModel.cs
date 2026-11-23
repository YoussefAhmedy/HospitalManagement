namespace HospitalManagement.Models;

/// <summary>Route-preserving pager so pagination never drops active filters.</summary>
public sealed class PagerModel
{
    public required Dictionary<string, string?> RouteValues { get; init; }
    public int Page { get; init; }
    public int TotalPages { get; init; }
    public int TotalCount { get; init; }
    public int PageSize { get; init; }

    public static PagerModel From(Dictionary<string, string?> routeValues, int page, int totalPages, int totalCount, int pageSize) =>
        new() { RouteValues = routeValues, Page = page, TotalPages = totalPages, TotalCount = totalCount, PageSize = pageSize };
}

public static class RouteValueExtensions
{
    public static Dictionary<string, string?> WithPage(this Dictionary<string, string?> values, int page)
    {
        var copy = new Dictionary<string, string?>(values) { ["page"] = page.ToString() };
        return copy;
    }
}
