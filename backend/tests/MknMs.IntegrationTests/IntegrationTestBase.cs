using Microsoft.EntityFrameworkCore;
using MknMs.Persistence;
using Xunit;

namespace MknMs.IntegrationTests;

/// <summary>
/// Base class for integration tests. Provides a per-class database
/// through <see cref="TestDatabaseFixture"/>.
/// </summary>
/// <remarks>
/// A test class inherits from this and declares
/// <c>: IntegrationTestBase(_fixture)</c>; xUnit injects the fixture.
/// Class-specific seed data goes in the derived class's
/// <see cref="InitializeAsync"/> after calling <c>base.InitializeAsync()</c>.
///
/// The derived class should not truncate tables. The database is
/// empty by construction — created blank and migrated in the
/// fixture's InitializeAsync, dropped in DisposeAsync. A test that
/// needs a clean slate for a specific seed simply calls
/// <see cref="ResetAsync"/> to remove all rows, or seeds against the
/// empty database directly.
///
/// The derived class may use <see cref="CreateContext"/> to obtain a
/// scoped context for a single operation; the base class's
/// <see cref="Db"/> property is a long-lived context for the class's
/// own seed and assertion paths.
/// </remarks>
public abstract class IntegrationTestBase : IAsyncLifetime
{
    private readonly TestDatabaseFixture _fixture;
    private MknDbContext? _db;

    protected IntegrationTestBase(TestDatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    /// <summary>
    /// The class-scoped <see cref="MknDbContext"/> against the
    /// per-class database. Created lazily on first access; disposed
    /// by the base class's <see cref="DisposeAsync"/>.
    /// </summary>
    protected MknDbContext Db =>
        _db ??= _fixture.CreateContext();

    /// <summary>
    /// Create a fresh <see cref="MknDbContext"/> for an operation
    /// that needs its own context. The caller owns it.
    /// </summary>
    protected MknDbContext CreateContext() => _fixture.CreateContext();

    public virtual Task InitializeAsync() => Task.CompletedTask;

    public virtual async Task DisposeAsync()
    {
        if (_db is not null)
        {
            await _db.DisposeAsync();
            _db = null;
        }
    }
}
