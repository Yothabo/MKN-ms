using Microsoft.EntityFrameworkCore;
using MknMs.Infrastructure.Persistence.Naming;

namespace MknMs.Infrastructure.Persistence;

/// <summary>
/// The EF Core DbContext for MKN-MS. Every table in the physical schema
/// is mapped through a DbSet here, and every constraint declared in the
/// specification is expressed by the corresponding
/// IEntityTypeConfiguration&lt;T&gt; class in the Configurations folder.
/// </summary>
/// <remarks>
/// Authority: docs/database/schema.md and docs/spec/system-design-spec.md §4, §15.
/// </remarks>
public class MknDbContext : DbContext
{
    public MknDbContext(DbContextOptions<MknDbContext> options)
        : base(options)
    {
    }

    // D1 — Role / Duty
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Duty> Duties => Set<Duty>();

    // D2 — Duty Rule
    public DbSet<DutyRule> DutyRules => Set<DutyRule>();

    // D3 — Branch / Time Slot / Service / Occurrence
    public DbSet<Branch> Branches => Set<Branch>();
    public DbSet<BranchTimeSlot> BranchTimeSlots => Set<BranchTimeSlot>();
    public DbSet<ServiceDefinition> ServiceDefinitions => Set<ServiceDefinition>();
    public DbSet<ServiceDefinitionDuty> ServiceDefinitionDuties => Set<ServiceDefinitionDuty>();
    public DbSet<ServiceSchedule> ServiceSchedules => Set<ServiceSchedule>();
    public DbSet<ServiceOccurrence> ServiceOccurrences => Set<ServiceOccurrence>();
    public DbSet<ServiceOccurrenceDuty> ServiceOccurrenceDuties => Set<ServiceOccurrenceDuty>();

    // D4 — Member
    public DbSet<Member> Members => Set<Member>();
    public DbSet<Admin> Admins => Set<Admin>();

    // D5 — Identifier History
    public DbSet<IdentifierHistory> IdentifierHistories => Set<IdentifierHistory>();

    // D6 — Eligibility
    public DbSet<Eligibility> Eligibilities => Set<Eligibility>();

    // D7 — Roster Assignment
    public DbSet<RosterAssignment> RosterAssignments => Set<RosterAssignment>();

    // D8 — Attendance Record
    public DbSet<AttendanceRecord> AttendanceRecords => Set<AttendanceRecord>();

    // D9 — Event / Program / Program Item / Event Duty
    public DbSet<Event> Events => Set<Event>();
    public DbSet<Program> Programs => Set<Program>();
    public DbSet<ProgramItem> ProgramItems => Set<ProgramItem>();
    public DbSet<EventDuty> EventDuties => Set<EventDuty>();

    // D10 — Config Lookups
    public DbSet<TimeOfDay> TimeOfDays => Set<TimeOfDay>();
    public DbSet<ServiceType> ServiceTypes => Set<ServiceType>();
    public DbSet<OutcomeState> OutcomeStates => Set<OutcomeState>();
    public DbSet<AssignmentStatus> AssignmentStatuses => Set<AssignmentStatus>();
    public DbSet<PermissionTier> PermissionTiers => Set<PermissionTier>();

    // D11 — System Setting
    public DbSet<SystemSetting> SystemSettings => Set<SystemSetting>();

    // D12 — Materializer Run
    public DbSet<MaterializerRun> MaterializerRuns => Set<MaterializerRun>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Apply snake_case naming to every table and column.
        // This maps C# property names (RoleId) to database names (role_id).
        modelBuilder.ApplySnakeCaseNamingConvention();

        // Applies every IEntityTypeConfiguration<T> found in this assembly.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(MknDbContext).Assembly);
    }
}
