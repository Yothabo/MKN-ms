var builder = WebApplication.CreateBuilder(args);

// Register the MknDbContext with the PostgreSQL provider.
var connectionString = builder.Configuration.GetConnectionString("MknDb")
    ?? throw new InvalidOperationException(
        "Connection string 'MknDb' is not configured. " +
        "Set it in appsettings.Development.json or via the " +
        "ConnectionStrings__MknDb environment variable.");

builder.Services.AddDbContext<MknDbContext>(options =>
    options.UseNpgsql(connectionString));

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

// Placeholder endpoint so the app is runnable. Real endpoints
// will be added as each of the twelve processes is implemented.
app.MapGet("/", () => new
{
    service = "MKN-MS API",
    status = "running",
    version = "0.1.0-prototype"
});

app.Run();
