using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;

namespace HospitalManagement.Infrastructure;

/// <summary>
/// Persists the interface language for the browser session. The value is a cookie holding a
/// culture name only — no user data, no personal identifiers.
/// </summary>
[ApiController]
[Route("culture")]
public sealed class CultureController : ControllerBase
{
    private static readonly string[] Supported = ["en", "ar"];

    [HttpGet("set")]
    public IActionResult Set(string culture, string? returnUrl = null)
    {
        var target = Supported.Contains(culture, StringComparer.OrdinalIgnoreCase) ? culture : "en";

        Response.Cookies.Append(
            CookieRequestCultureProvider.DefaultCookieName,
            CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(target)),
            new CookieOptions
            {
                Expires = DateTimeOffset.UtcNow.AddYears(1),
                IsEssential = true,
                HttpOnly = false,
                SameSite = SameSiteMode.Lax,
                Secure = Request.IsHttps
            });

        return LocalRedirect(SafeReturnUrl(returnUrl));
    }

    /// <summary>Only same-site relative paths are accepted, preventing an open redirect.</summary>
    private static string SafeReturnUrl(string? returnUrl) =>
        !string.IsNullOrWhiteSpace(returnUrl)
        && returnUrl.StartsWith('/')
        && !returnUrl.StartsWith("//", StringComparison.Ordinal)
        && !returnUrl.StartsWith("/\\", StringComparison.Ordinal)
            ? returnUrl
            : "/";
}
