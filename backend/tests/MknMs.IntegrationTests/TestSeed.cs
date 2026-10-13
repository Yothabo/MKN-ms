
using Microsoft.EntityFrameworkCore;
using MknMs.Domain.D4_Member;
using MknMs.Persistence;

namespace MknMs.IntegrationTests;

/// <summary>
/// Small helpers shared by test fixtures.
/// </summary>
/// <remarks>
/// Member.MemberStatusId is non-null as of migration Step 7. Every
/// fixture that creates a Member must supply a MemberStatusId. This
/// helper ensures a default MemberStatus row exists and returns its
/// id.
/// </remarks>
internal static class TestSeed
{
    public static async Task<int> EnsureDefaultMemberStatusAsync(MknDbContext db)
    {
        var existing = await db.MemberStatuses
            .FirstOrDefaultAsync(s => s.IsDefault);

        if (existing is not null)
        {
            return existing.MemberStatusId;
        }

        var status = new MemberStatus
        {
            Name = "Active",
            IsRosterable = true,
            IsDefault = true,
            IsDeleted = false,
        };
        db.MemberStatuses.Add(status);
        await db.SaveChangesAsync();
        return status.MemberStatusId;
    }

    public static async Task EnsureRequiredSettingsAsync(MknDbContext db)
    {
        if (!await db.SystemSettings.AnyAsync(s => s.Key == "ApplicationTimeZone"))
        {
            db.SystemSettings.Add(new MknMs.Domain.D11_SystemSetting.SystemSetting
            {
                Key = "ApplicationTimeZone",
                Value = "Africa/Johannesburg",
                Required = true,
            });
        }

        if (!await db.SystemSettings.AnyAsync(s => s.Key == "AttendanceRegisterEnabled"))
        {
            db.SystemSettings.Add(new MknMs.Domain.D11_SystemSetting.SystemSetting
            {
                Key = "AttendanceRegisterEnabled",
                Value = "true",
                Required = true,
            });
        }

        await db.SaveChangesAsync();
    }
}
