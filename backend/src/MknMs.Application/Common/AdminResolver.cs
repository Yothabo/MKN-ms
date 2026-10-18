namespace MknMs.Application.Common;

/// <summary>
/// Resolves a caller-supplied admin id to an Admin row and checks
/// that the row is active.
/// </summary>
/// <remarks>
/// §9.7 and C6: every process that acts on behalf of an administrator
/// must resolve the acting admin id to an <see cref="Admin"/> row whose
/// <c>IsActive</c> is true. A missing row is a referenced-entity-missing
/// failure; an inactive row is a referenced-entity-inactive failure.
///
/// Shared by every configuration service that writes an attribution
/// field (created-by, initiated-by, authorised-by, assigned-by). The
/// approver resolution inside ConfigurationLifecycleService is *not*
/// routed through this helper, because its error text names the approver
/// specifically, not the acting admin.
/// </remarks>
internal static class AdminResolver
{
    public static async Task<ConfigurationError?> ResolveActiveAsync(
        MknDbContext db,
        int adminId,
        CancellationToken cancellationToken)
    {
        var admin = await db.Admins
            .FirstOrDefaultAsync(a => a.AdminId == adminId, cancellationToken);

        if (admin is null)
        {
            return new ConfigurationError(
                ConfigurationErrorCodes.ReferencedEntityMissing,
                $"Admin {adminId} does not exist.");
        }

        if (!admin.IsActive)
        {
            return new ConfigurationError(
                ConfigurationErrorCodes.ReferencedEntityInactive,
                $"Admin {adminId} is not active.");
        }

        return null;
    }
}
