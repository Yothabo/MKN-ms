using Microsoft.EntityFrameworkCore;
using MknMs.Persistence;
using Xunit;

namespace MknMs.IntegrationTests;

/// <summary>
/// Base class for integration tests. Owns a per-class
/// <see cref="TestDatabaseFixture"/>: created in
/// <see cref="InitializeAsync"/>, dropped in
/// <see cref="DisposeAsync"/>.
/// </summary>
/// <remarks>
/// The fixture is owned by the base rather than injected through
/// xUnit's <c>IClassFixture&lt;T&gt;</c>. The inherited-fixture form
/// trips xUnit's <c>xUnit1041</c> analyzer, which requires the
/// fixture source to be declared on the class itself. Owning the
/// fixture inside the base avoids the analyzer's requirement without
/// changing the isolation property: each derived class still gets its
/// own database, and the database is dropped before the next class's
/// fixture is created (parallelisation is disabled at the assembly
/// level).
///
/// The derived class should not truncate tables. The database is
/// empty by construction.
/// </remarks>
public abstract class IntegrationTestBase : IAsyncLifetime
{
    private TestDatabaseFixture? _fixture;
    private MknDbContext? _db;

    /// <summary>
    /// The class-scoped <see cref="MknDbContext"/> against the
    /// per-class database. Created lazily on first access; disposed
    /// by the base class's <see cref="DisposeAsync"/>.
    /// </summary>
    protected MknDbContext Db =>
        _db ??= _fixture!.CreateContext();

    /// <summary>
    /// Create a fresh <see cref="MknDbContext"/> for an operation
    /// that needs its own context. The caller owns it.
    /// </summary>
    protected MknDbContext CreateContext() => _fixture!.CreateContext();

    public async Task InitializeAsync()
    {
        _fixture = new TestDatabaseFixture();
        await _fixture.InitializeAsync();
        await OnInitializedAsync();
    }

    /// <summary>
    /// Hook for class-specific initialisation. Override this method in a
    /// derived class instead of overriding <see cref="InitializeAsync"/>.
    /// </summary>
    /// <remarks>
    /// <see cref="InitializeAsync"/> creates the per-class
    /// <see cref="TestDatabaseFixture"/> and is the only place that does so.
    /// A derived class that overrides <c>InitializeAsync</c> without calling
    /// the base implementation leaves the fixture null, and every access to
    /// <see cref="Db"/> throws <see cref="NullReferenceException"/>. Making
    /// the lifecycle methods non-virtual and providing this hook removes the
    /// possibility of that mistake: the fixture is created by the base class
    /// regardless of what a derived class does.
    ///
    /// A class that needs class-specific setup should override this method
    /// and leave <see cref="InitializeAsync"/> alone.
    /// </remarks>
    protected virtual Task OnInitializedAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        if (_db is not null)
        {
            await _db.DisposeAsync();
            _db = null;
        }

        if (_fixture is not null)
        {
            await _fixture.DisposeAsync();
            _fixture = null;
        }
    }
}
