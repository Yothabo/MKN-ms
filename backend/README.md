# MKN-MS — Backend

ASP.NET Core Web API implementing the twelve processes defined in the System Design Specification.

## Authority

The specification is authoritative. It lives at `../docs/spec/system-design-spec.md`. Where this README or the code conflicts with the specification, the specification wins.

## Stack

- .NET 8 (LTS) — ASP.NET Core Web API
- EF Core 8 — database access, migrations
- PostgreSQL 17+ — target database
- Quartz.NET — scheduled jobs (materializer, confirmation timeouts, fill-status sweeps)
- FluentValidation — request and domain validation
- Swashbuckle — OpenAPI generation

## Layout

    backend/
    ├── MknMs.sln
    ├── src/
    │   ├── MknMs.Api/             HTTP endpoints; one controller per process group
    │   ├── MknMs.Application/     The twelve processes; one folder per process
    │   ├── MknMs.Domain/          Entities, value objects, domain rules
    │   └── MknMs.Infrastructure/  EF Core, PostgreSQL, Quartz, notification transport
    └── tests/
        ├── MknMs.UnitTests/
        └── MknMs.IntegrationTests/

Each of the twelve processes from the specification has a corresponding folder under `MknMs.Application/`. See the process documentation at `../docs/processes/` for details.

## Build

From inside `backend/`:

    dotnet build

## Run

From inside `backend/`:

    dotnet run --project src/MknMs.Api

The API listens on `http://localhost:5000` (or the port configured in `launchSettings.json`). OpenAPI is served at `/swagger`.

## Database

The development database runs on the Termux host, outside proot, listening on `127.0.0.1:5432`.

- Database: `mkn_dev`
- User: `mkn_dev`

Migrations:

    dotnet ef migrations add <Name> --project src/MknMs.Infrastructure --startup-project src/MknMs.Api
    dotnet ef database update --project src/MknMs.Infrastructure --startup-project src/MknMs.Api

## Tests

    dotnet test
