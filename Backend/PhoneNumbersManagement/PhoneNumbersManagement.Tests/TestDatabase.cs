using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using PhoneNumbersManagement.Application;
using PhoneNumbersManagement.Infrastructure;

namespace PhoneNumbersManagement.Tests;

internal sealed class TestDatabase : IDisposable
{
    public SqliteConnection Connection { get; } = new("Data Source=:memory:");

    public RegistryDbContext Db
    {
        get;
    }

    public MutableClock Clock { get; } = new();

    public RegistryService Service
    {
        get;
    }

    public TestDatabase()
    {
        Connection.Open();
        Db = new(CreateOptions(Connection));
        Db.Database.EnsureCreated();
        Service = new(new EfRegistryStore(Db), Clock);
    }

    public static DbContextOptions<RegistryDbContext> CreateOptions(SqliteConnection connection) => new DbContextOptionsBuilder<RegistryDbContext>()
        .UseSqlite(connection).ReplaceService<IModelCustomizer, SqliteTestModelCustomizer>().Options;

    public void Dispose()
    {
        Db.Dispose();
        Connection.Dispose();
    }
}

public sealed class SqliteTestModelCustomizer(ModelCustomizerDependencies dependencies) : ModelCustomizer(dependencies)
{
    public override void Customize(ModelBuilder modelBuilder, DbContext context)
    {
        base.Customize(modelBuilder, context);

        // SQLite lacks native timestamptz comparisons. Production migrations use PostgreSQL timestamptz.
        modelBuilder.Entity<Domain.Reservation>().Property(x => x.Expires)
            .HasConversion(value => value.HasValue ? value.Value.UtcTicks : (long?)null,
                value => value.HasValue ? new DateTimeOffset(value.Value, TimeSpan.Zero) : null);
        modelBuilder.Entity<Domain.PhoneNumber>().Property(x => x.ScheduledUnassignment)
            .HasConversion(value => value.HasValue ? value.Value.UtcTicks : (long?)null,
                value => value.HasValue ? new DateTimeOffset(value.Value, TimeSpan.Zero) : null);
    }
}

internal sealed class MutableClock : TimeProvider
{
    public DateTimeOffset Now { get; set; } = new(2026, 10, 20, 12, 0, 0, TimeSpan.Zero);

    public override DateTimeOffset GetUtcNow() => Now;
}

internal sealed class FakeTeams : ITeamsDirectory
{
    public IReadOnlyList<TeamsAssignment> Assignments { get; set; } = [];

    public DirectoryUserState State { get; set; } = DirectoryUserState.Exists;

    public bool Fail { get; set; }

    public Task<IReadOnlyList<TeamsAssignment>> GetAssignmentsAsync(CancellationToken ct) =>
        Fail ? throw new HttpRequestException("Simulated unavailable Graph") : Task.FromResult(Assignments);

    public Task<DirectoryUserState> GetUserStateAsync(Guid userId, CancellationToken ct) =>
        Fail ? throw new HttpRequestException("Simulated unavailable Graph") : Task.FromResult(State);
}
