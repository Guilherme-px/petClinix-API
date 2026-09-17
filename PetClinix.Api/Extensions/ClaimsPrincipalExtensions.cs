using System.Security.Claims;

namespace PetClinix.Api.Extensions;

public static class ClaimsPrincipalExtensions
{
    public static bool TryGetClinicId(this ClaimsPrincipal user, out Guid clinicId)
    {
        clinicId = Guid.Empty;
        return Guid.TryParse(user.FindFirst("clinic_id")?.Value, out clinicId);
    }

    public static bool TryGetUserId(this ClaimsPrincipal user, out Guid userId)
    {
        var claim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? user.FindFirst("sub")?.Value;
        userId = Guid.Empty;
        return Guid.TryParse(claim, out userId);
    }
}