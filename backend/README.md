# MKN-MS — Backend

ASP.NET Core Web API implementing the twelve processes defined in the System Design Specification.

## Authority

The specification is authoritative. It lives at `../docs/spec/system-design-spec.md`. Where this README or the code conflicts with the specification, the specification wins.

## Development and test environments

This project is developed on an Android device with two distinct environments:

- **Development — inside proot Debian.** Editing, building, migrations, and the EF Core CLI. The .NET SDK is installed at `/root/.dotnet` inside proot.
- **Testing — native Termux.** `dotnet test` runs on the Termux side, outside proot. The proot environment's fork/exec translation does not support the `vstest.console` child process that runs test host binaries, so tests must be executed from Termux directly.

Both environments share the same source tree (via `proot-distro`'s bind mount) and the same PostgreSQL instance (running on Termux).

To build:

    # From inside proot Debian
    cd /host-home/MKN-ms/backend
    dotnet build

To run tests:

    # From Termux (not inside proot)
    cd ~/MKN-ms/backend
    dotnet test tests/MknMs.IntegrationTests

The two-environment split is a deliberate consequence of Android's syscall surface and proot's incomplete emulation of it, not a defect in the project. Every workaround that makes each environment work (GC heap cap, `DOTNET_ROOT`, host path symlinks) is required for its own purpose and stays in place.

### Environment requirements

Two properties of this environment must be set for the build and the tests to work correctly. Both are consequences of running .NET on Android/Termux, not of the code.

**GC heap hard limit (proot).** The default value, set in proot's `/root/.bashrc`, is `0x20000000` (512 MB). This is too small to compile the current solution: the build fails with `MSB4166: Child node exited prematurely` or `MSB6006: "csc.dll" exited with code 139` when the Roslyn compiler runs out of memory. The value must be raised to at least `0x60000000` (1.5 GB):

    # In proot's /root/.bashrc
    export DOTNET_GCHeapHardLimit=0x60000000

If a build fails with either of those errors, the heap limit is the cause. Raising it further — to `0x80000000` (2 GB) — is the next step if 1.5 GB is still insufficient; 2 GB is the practical ceiling on the device.

**TZDIR (Termux).** Termux does not ship the standard IANA tzdata. The .NET runtime resolves `TimeZoneInfo.FindSystemTimeZoneById("Africa/Johannesburg")` from `/usr/share/zoneinfo` by default, which does not exist on the Termux side. The PostgreSQL package ships a compatible tzdata tree at `$PREFIX/share/postgresql/timezone`. Pointing `TZDIR` at that tree makes the resolver work:

    # In the Termux shell that runs the tests
    export TZDIR=$PREFIX/share/postgresql/timezone

Without `TZDIR`, the two `TimeZoneResolverTests` that exercise the configured-timezone path fail; the code falls back to UTC as designed. Set `TZDIR` before `dotnet test`, or add it to the Termux `.bashrc`.

**ApplicationTimeZone (lifecycle).** The scheduler and the processes both derive their notion of "today" from `SystemSetting.ApplicationTimeZone`. The processes resolve it on every run, so a change takes effect for them on their next invocation. The Quartz trigger timezones are resolved once at application startup via `TimeZoneResolver.ResolveFromConnectionAsync`, so a change to `ApplicationTimeZone` affects Quartz trigger times only on the next application restart. If an administrator changes the setting, the processes pick it up immediately; the scheduler continues using the previous value until the application is restarted.

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
    │   ├── MknMs.Persistence/     EF Core, PostgreSQL, migrations
    │   └── MknMs.Infrastructure/  External integrations (Quartz scheduler, notification transport)
    └── tests/
        ├── MknMs.UnitTests/
        └── MknMs.IntegrationTests/

Each of the twelve processes from the specification has a corresponding folder under `MknMs.Application/Processes/`. See the process documentation at `../docs/processes/` for details.

## Dependency direction

    Domain  ←  Persistence  ←  Application  ←  Infrastructure  ←  API

Every project references only projects to its left in this diagram. This is the standard Clean Architecture dependency direction; no project may reference one to its right.

- `Domain` — entities. No project references.
- `Persistence` — `MknDbContext`, entity configurations, migrations, the snake_case naming convention.
- `Application` — the twelve processes; depends on `Domain` and `Persistence`.
- `Infrastructure` — external integrations. Holds the Quartz job classes and the scheduler registration.
- `API` — hosts the HTTP surface and the composition root.

## Scheduling

Four processes have scheduled triggers, registered with Quartz.NET in `MknMs.Infrastructure/Scheduling/QuartzSchedulerRegistration.cs` and started by `Quartz.Extensions.Hosting` from `Program.cs`:

| Job | Process | Cadence |
| --- | --- | --- |
| `MaterializeOccurrencesJob` | 11.0 | Daily, 02:00 |
| `GenerateAssignmentJob` | 5.0 | Daily, 03:00 |
| `ManageConfirmationTimeoutJob` | 7.0 | Hourly |
| `EvaluateFillStatusSweepJob` | 10.0 | Hourly, at :30 |

All four job classes are decorated with `[DisallowConcurrentExecution]` so Quartz never runs two instances of the same job at once. Process 11.0 additionally takes a PostgreSQL session-level advisory lock, so that a manual trigger of the materializer cannot overlap a scheduled run.

The trigger times are evaluated in `SystemSetting.ApplicationTimeZone`, resolved at application startup. See the lifecycle note in the environment requirements above.

The cadences are implementation choices the specification does not fix beyond the fact that 11.0 runs daily and the others run periodically.

## Build

From inside `backend/`:

    dotnet build

If the build fails with `MSB4166` or `MSB6006`, see the environment requirements above — the GC heap limit is too small.

## Run

From inside `backend/`:

    dotnet run --project src/MknMs.Api

The API listens on `http://localhost:5000` by default. If port 5000 is already in use, Kestrel will select an alternate port and print it in the startup log — look for the line `Now listening on: http://localhost:NNNN` and use that port in all subsequent calls.

OpenAPI is served at `/swagger` when the environment is `Development`.

The Quartz scheduler starts with the application. The four jobs fire on their configured schedules.

## Database

The development database runs on the Termux host, outside proot, listening on `127.0.0.1:5432`.

- Database: `mkn_dev`
- Test database: `mkn_test`
- User: `mkn_dev`

Migrations:

    dotnet ef migrations add <Name> --project src/MknMs.Persistence --startup-project src/MknMs.Api
    dotnet ef database update --project src/MknMs.Persistence --startup-project src/MknMs.Api

Seed data for development:

    psql -h 127.0.0.1 -U mkn_dev -d mkn_dev -f scripts/seed-development.sql
    psql -h 127.0.0.1 -U mkn_dev -d mkn_dev -f scripts/seed-assignment.sql

Both seed scripts are idempotent — safe to re-run. Every insert is guarded by a `NOT EXISTS` check on the entity's natural key.

## Tests

From Termux (not inside proot), with `TZDIR` set as described in the environment requirements:

    export TZDIR=$PREFIX/share/postgresql/timezone
    cd ~/MKN-ms/backend
    dotnet test tests/MknMs.IntegrationTests

The integration tests connect to the `mkn_test` database. Ensure it exists and has the current schema applied before running them.

## Process implementation pattern

Every process from the specification follows the same implementation sequence:

1. **Read the process contract** in `../docs/processes/`.
2. **Identify the entities** the process reads and writes.
3. **Implement the service** in `src/MknMs.Application/Processes/...`.
4. **Register the service** in `src/MknMs.Api/Program.cs`.
5. **Add an endpoint** in `src/MknMs.Api/Endpoints/`.
6. **Seed data** for testing in `scripts/`.
7. **Write integration tests** in `tests/MknMs.IntegrationTests/`.
8. **Run tests from Termux.**
9. **Commit.**

The pattern is established by process 11.0 Materialize Occurrences — see `src/MknMs.Application/Processes/Operations/11.0_MaterializeOccurrences/` for the reference implementation.
