namespace MknMs.Application.Common;

/// <summary>
/// Age arithmetic shared by every process that evaluates a member's
/// date of birth against a "today". §10.1.5's AgeRange and Youth
/// criteria and §13.6's absence check all derive age the same way;
/// this type is the single source of that derivation.
/// </summary>
/// <remarks>
/// Completed years: a member is N years old the moment the Nth
/// anniversary of DateOfBirth has been reached. Leap-day birthdays
/// follow the C# TimeOnly/DateOnly convention — the anniversary is
/// observed on Feb 28 in non-leap years.
///
/// Specification: §10.1.5 (age calculation), §13.6.
/// </remarks>
public static class MemberAge
{
    public static int CompletedYears(DateOnly dateOfBirth, DateOnly today)
    {
        var age = today.Year - dateOfBirth.Year;
        if (dateOfBirth > today.AddYears(-age))
        {
            age--;
        }
        return age;
    }
}
