using System.Security.Claims;
using Hospital.BLL.Helpers;
using Hospital.BLL.Permissions;
using Microsoft.AspNetCore.Authentication;

namespace HospitalManagement.Infrastructure;

/// <summary>
/// Projects the signed-in user's roles into explicit permission claims once per request.
/// Because the projection happens server-side on every authenticated request, revoking a
/// role takes effect immediately without re-issuing the cookie.
/// </summary>
public sealed class PermissionClaimsTransformation : IClaimsTransformation
{
    public Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        if (principal.Identity?.IsAuthenticated != true)
        {
            return Task.FromResult(principal);
        }

        if (principal.HasClaim(claim => claim.Type == AppClaimTypes.Permission))
        {
            return Task.FromResult(principal);
        }

        var identity = (ClaimsIdentity)principal.Identity!;
        var roles = principal.FindAll(ClaimTypes.Role).Select(claim => claim.Value).Distinct().ToList();

        foreach (var role in roles)
        {
            foreach (var permission in RolePermissions.ForName(role))
            {
                if (!principal.HasClaim(AppClaimTypes.Permission, permission))
                {
                    identity.AddClaim(new Claim(AppClaimTypes.Permission, permission));
                }
            }

            if (RolePermissions.IsClinician(ParseRole(role)))
            {
                if (!principal.HasClaim(AppClaimTypes.Clinical, "true"))
                {
                    identity.AddClaim(new Claim(AppClaimTypes.Clinical, "true"));
                }
            }
        }

        return Task.FromResult(principal);
    }

    private static Role ParseRole(string roleName) =>
        Enum.TryParse<Role>(roleName, ignoreCase: true, out var role) ? role : Role.Patient;
}
