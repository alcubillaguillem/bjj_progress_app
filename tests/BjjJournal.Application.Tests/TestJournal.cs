using BjjJournal.Application.Abstractions;
using BjjJournal.Application.Persistence;
using BjjJournal.Application.Services;
using Microsoft.Extensions.Time.Testing;

namespace BjjJournal.Application.Tests;

/// <summary>Montaje común: almacén en memoria y reloj falso (miércoles 11/11/2026 12:00 UTC, zona UTC).</summary>
public sealed class TestJournal
{
    public TestJournal()
    {
        Time = new FakeTimeProvider(new DateTimeOffset(2026, 11, 11, 12, 0, 0, TimeSpan.Zero));
        Time.SetLocalTimeZone(TimeZoneInfo.Utc);
        Store = new InMemoryStore();
        Context = new JournalContext(Store);
        Profiles = new ProfileService(Context, Time);
        Positions = new PositionService(Context, Time);
        Techniques = new TechniqueService(Context, Time);
        Sessions = new SessionService(Context, Time);
        Stats = new StatsService(Context, Time);
        Backup = new BackupService(Context, Time);
    }

    public FakeTimeProvider Time { get; }
    public InMemoryStore Store { get; }
    public JournalContext Context { get; }
    public ProfileService Profiles { get; }
    public PositionService Positions { get; }
    public TechniqueService Techniques { get; }
    public SessionService Sessions { get; }
    public StatsService Stats { get; }
    public BackupService Backup { get; }

    public sealed class InMemoryStore : IAppDataStore
    {
        public string? Json { get; set; }
        public int Writes { get; private set; }

        public Task<string?> ReadAsync(CancellationToken ct = default) => Task.FromResult(Json);

        public Task WriteAsync(string json, CancellationToken ct = default)
        {
            Json = json;
            Writes++;
            return Task.CompletedTask;
        }
    }
}
