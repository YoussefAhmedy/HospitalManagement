namespace Hospital.BLL.Common;

/// <summary>
/// Normalised list query parameters shared by every list screen so paging, searching and
/// filtering behave identically everywhere.
/// </summary>
public sealed class ListQuery
{
    public string? Term { get; set; }
    public string? Status { get; set; }
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
    public string? DoctorId { get; set; }
    public string? PatientId { get; set; }
    public int? DepartmentId { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;

    public DateRange Range => new(From, To);

    public ListQuery Normalised()
    {
        Page = Page < 1 ? 1 : Page;
        PageSize = PageSize is < 10 or > 100 ? 20 : PageSize;
        Term = string.IsNullOrWhiteSpace(Term) ? null : Term.Trim();
        return this;
    }

    public Dictionary<string, string?> ToRouteValues()
    {
        var values = new Dictionary<string, string?>();
        if (Term is not null) values["term"] = Term;
        if (Status is not null) values["status"] = Status;
        if (From is not null) values["from"] = From.Value.ToString("yyyy-MM-dd");
        if (To is not null) values["to"] = To.Value.ToString("yyyy-MM-dd");
        if (DoctorId is not null) values["doctorId"] = DoctorId;
        if (PatientId is not null) values["patientId"] = PatientId;
        if (DepartmentId is not null) values["departmentId"] = DepartmentId.Value.ToString();
        values["pageSize"] = PageSize.ToString();
        return values;
    }
}
