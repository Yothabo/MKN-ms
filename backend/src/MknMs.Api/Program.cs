using MknMs.Api.Endpoints;
using MknMs.Application.Processes.Operations.MaterializeOccurrences;

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

// Register the twelve processes as they are implemented.
// Process 11.0 — Materialize Occurrences.
builder.Services.AddScoped<
    IMaterializeOccurrencesService,
    MaterializeOccurrencesService>();

// Standard API services.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// Placeholder root endpoint.
app.MapGet("/", () => new
{
    service = "MKN-MS API",
    status = "running",
    version = "0.1.0-prototype"
});

// Process endpoints.
app.MapMaterializeOccurrencesEndpoint();

app.Run();
