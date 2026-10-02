using Hangfire.Dashboard;

namespace HospitalManagement.Infrastructure;

public sealed class AdminDashboardAuthorizationFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context) =>
        context.GetHttpContext().User.Identity?.IsAuthenticated == true &&
        context.GetHttpContext().User.IsInRole("Admin");
}
