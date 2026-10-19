namespace MknMs.Application.Common;

/// <summary>
/// Tenure arithmetic: calendar days between a member's join date and
/// a "today". §10.1.5's Tenure criterion uses this.
/// </summary>
/// <remarks>
/// Days are measured on <see cref="DateOnly.DayNumber"/>, which is a
/// pure day count with no time or timezone component.
///
/// Specification: §10.1.5 (Tenure criterion).
/// </remarks>
public static class MemberTenure
{
    public static int DaysSince(DateOnly joinDate, DateOnly today) =>
        today.DayNumber - joinDate.DayNumber;
}
