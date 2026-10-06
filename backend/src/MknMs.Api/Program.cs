using MknMs.Api.Endpoints;
using MknMs.Application.Common;
using MknMs.Application.Processes.Configuration.ConfigureBranch;
using MknMs.Application.Processes.Configuration.ConfigureDuty;
using MknMs.Application.Processes.Configuration.ConfigureDutyRules;
using MknMs.Application.Processes.Configuration.ConfigureRole;
using MknMs.Application.Processes.Configuration.ConfigureServiceDefinition;
using MknMs.Application.Processes.Configuration.ConfigureServiceDutyAndSchedule;
using MknMs.Application.Processes.Configuration.ConfigureTimeSlot;
using MknMs.Application.Processes.Configuration.Lookup_ServiceType;
using MknMs.Application.Processes.Configuration.Lookup_TimeOfDay;
using MknMs.Application.Processes.Configuration.ManageEligibility;
using MknMs.Application.Processes.Configuration.ManageEvent;
using MknMs.Application.Processes.Configuration.ManageEventDuty;
using MknMs.Application.Processes.Configuration.ManageIdentifierHistory;
using MknMs.Application.Processes.Configuration.ManageMemberRecord;
using MknMs.Application.Processes.Configuration.ManageProgram;
using MknMs.Application.Processes.Configuration.ManageProgramItem;
using MknMs.Application.Processes.Operations.DispatchNotification;
using MknMs.Application.Processes.Operations.EvaluateFillStatus;
using MknMs.Application.Processes.Operations.RecordAttendance;
using MknMs.Application.Processes.Operations.CreateManualAssignment;
using MknMs.Application.Processes.Operations.GenerateAssignment;
using MknMs.Application.Processes.Operations.ManageConfirmation;
using MknMs.Application.Processes.Operations.MaterializeOccurrences;
using MknMs.Infrastructure.Scheduling;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);

// Register the MknDbContext with the PostgreSQL provider.
var connectionString = builder.Configuration.GetConnectionString("MknDb")
    ?? throw new InvalidOperationException(
        "Connection string 'MknDb' is not configured. " +
        "Set it in appsettings.Development.json or via the " +
        "ConnectionStrings__MknDb environment variable.");

builder.Services.AddDbContext<MknDbContext>(options =>
    options.UseNpgsql(connectionString));

// Register TimeProvider for deterministic, testable clock access.
builder.Services.AddSingleton(TimeProvider.System);

// ---------------------------------------------------------------------
// Configuration layer services.
// ---------------------------------------------------------------------

// Lookups (D10).
builder.Services.AddScoped<ITimeOfDayService, TimeOfDayService>();
builder.Services.AddScoped<IServiceTypeService, ServiceTypeService>();

// 1.0 Configure Vocabulary.
builder.Services.AddScoped<IConfigureRoleService, ConfigureRoleService>();
builder.Services.AddScoped<IConfigureDutyService, ConfigureDutyService>();
builder.Services.AddScoped<IConfigureBranchService, ConfigureBranchService>();
builder.Services.AddScoped<IConfigureTimeSlotService, ConfigureTimeSlotService>();
builder.Services.AddScoped<IConfigureServiceDefinitionService, ConfigureServiceDefinitionService>();
builder.Services.AddScoped<IConfigureServiceDutyAndScheduleService, ConfigureServiceDutyAndScheduleService>();

// 2.0 Configure Duty Rules.
builder.Services.AddScoped<IConfigureDutyRulesService, ConfigureDutyRulesService>();

// 3.0 Manage Membership.
builder.Services.AddScoped<IManageMemberRecordService, ManageMemberRecordService>();
builder.Services.AddScoped<IManageIdentifierHistoryService, ManageIdentifierHistoryService>();

// 4.0 Manage Eligibility.
builder.Services.AddScoped<IManageEligibilityService, ManageEligibilityService>();

// 8.0 Manage Events and Programs.
builder.Services.AddScoped<IManageEventService, ManageEventService>();
builder.Services.AddScoped<IManageProgramService, ManageProgramService>();
builder.Services.AddScoped<IManageProgramItemService, ManageProgramItemService>();
builder.Services.AddScoped<IManageEventDutyService, ManageEventDutyService>();

// ---------------------------------------------------------------------
// Operations layer services.
// ---------------------------------------------------------------------

// 5.0 Generate Assignment.
builder.Services.AddScoped<IGenerateAssignmentService, GenerateAssignmentService>();

// 7.0 Manage Confirmation.
builder.Services.AddScoped<IManageConfirmationService, ManageConfirmationService>();

// 9.0 Dispatch Notification.
builder.Services.AddScoped<IDispatchNotificationService, DispatchNotificationService>();

// 10.0 Evaluate Fill Status.
builder.Services.AddScoped<IEvaluateFillStatusService, EvaluateFillStatusService>();

// 6.0 Record Attendance.
builder.Services.AddScoped<IRecordAttendanceService, RecordAttendanceService>();

// 12.0 Create Manual Assignment.
builder.Services.AddScoped<ICreateManualAssignmentService, CreateManualAssignmentService>();

// 11.0 Materialize Occurrences.
builder.Services.AddScoped<IMaterializeOccurrencesService, MaterializeOccurrencesService>();

// Standard API services.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

{
    await using var timeZoneConnection = new NpgsqlConnection(connectionString);
    var applicationTimeZone = await TimeZoneResolver.ResolveFromConnectionAsync(timeZoneConnection);
    builder.Services.AddMknScheduledJobs(applicationTimeZone);
}

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// Root endpoint.
app.MapGet("/", () => new
{
    service = "MKN-MS API",
    status = "running",
    version = "0.1.0-prototype"
});

// Process endpoints.
app.MapMaterializeOccurrencesEndpoint();
app.MapConfigurationEndpoints();
app.MapGenerateAssignmentEndpoint();
app.MapManageConfirmationEndpoint();
app.MapEvaluateFillStatusEndpoint();
app.MapRecordAttendanceEndpoint();
app.MapCreateManualAssignmentEndpoint();

app.Run();

/// <summary>
/// Exposes the implicit top-level Program class to the integration test
/// project, so WebApplicationFactory&lt;Program&gt; can build the real host.
/// </summary>
public partial class Program { }
