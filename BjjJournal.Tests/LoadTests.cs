using System.Diagnostics;
using System.Text.Json;
using Microsoft.JSInterop;
using Xunit.Abstractions;

namespace BjjJournal.Tests;

/// <summary>
/// Pruebas de carga de la lógica local: cuánto aguanta el diario cuando crece.
/// No hay servidor propio, así que la "carga" es el tamaño del diario y el número de operaciones por segundo.
/// Los resultados se imprimen (dotnet test --logger "console;verbosity=detailed") y están en docs/load-tests.md.
/// </summary>
[Trait("Category", "Load")]
public class LoadTests(ITestOutputHelper output)
{
    /// <summary>Límite de localStorage en Chrome, Edge y Safari: unos 5 millones de caracteres por web.</summary>
    private const int LocalStorageLimitChars = 5_000_000;

    private static readonly DateOnly Today = new(2026, 11, 25);

    [Theory]
    [InlineData(100)]
    [InlineData(1_000)]
    [InlineData(5_000)]
    [InlineData(10_000)]
    public async Task SaveSession_Throughput_And_Memory(int sessions)
    {
        var (journal, storage) = await BuildJournal(sessions);
        await journal.SaveSessionAsync(FakeSession(journal, Today, 999)); // calentamiento: la primera llamada compila el código
        var memoryBefore = GC.GetTotalMemory(forceFullCollection: true);

        // Guardar 50 sesiones nuevas, como haría el usuario. Cada guardado serializa el diario entero (igual que en el navegador).
        const int saves = 50;
        var watch = Stopwatch.StartNew();
        for (var i = 0; i < saves; i++)
        {
            await journal.SaveSessionAsync(FakeSession(journal, Today, i));
        }

        watch.Stop();

        var msPerSave = watch.Elapsed.TotalMilliseconds / saves;
        var savesPerSecond = saves / watch.Elapsed.TotalSeconds;
        var memoryMb = (GC.GetTotalMemory(forceFullCollection: true) - memoryBefore + storage.LastValue.Length * 2) / 1024.0 / 1024.0;
        var jsonMb = storage.LastValue.Length / 1_000_000.0;
        output.WriteLine($"{sessions,6} sesiones | {msPerSave,7:F2} ms/guardado | {savesPerSecond,7:F0} guardados/s | JSON {jsonMb,5:F2} M caracteres | {memoryMb,6:F1} MB | localStorage {(storage.LastValue.Length <= LocalStorageLimitChars ? "OK" : "LLENO")}");

        // Con un diario normal (hasta 1.000 sesiones) guardar tiene que ser instantáneo para el usuario.
        if (sessions <= 1_000)
        {
            Assert.True(msPerSave < 100, $"Guardar tarda {msPerSave:F1} ms");
        }
    }

    [Fact]
    public async Task LocalStorage_Limit_HowManySessionsFit()
    {
        var (_, storage) = await BuildJournal(1_000);
        var charsPerSession = (double)storage.LastValue.Length / 1_000;
        var maxSessions = (int)(LocalStorageLimitChars / charsPerSession);

        output.WriteLine($"{charsPerSession:F0} caracteres por sesión -> caben unas {maxSessions} sesiones ({maxSessions / 150.0:F1} años entrenando 3 días por semana)");

        Assert.True(maxSessions > 1_000, "Un diario de varios años tiene que caber en localStorage");
    }

    [Fact]
    public async Task Stats_HomeScreen_With10000Sessions()
    {
        var (journal, _) = await BuildJournal(10_000);
        var sessions = journal.Sessions.ToList();

        // Lo que calcula la pantalla de inicio al abrirse.
        var watch = Stopwatch.StartNew();
        Stats.Streak(sessions, Today);
        Stats.Weekly(sessions, Today);
        Stats.SatisfactionTrend(sessions, Today);
        Stats.TopTechniques(journal);
        Stats.Submissions(journal, sessions, RollEventKind.SubmissionFor);
        Stats.Gaps(journal, Today);
        Stats.LevelFor(Stats.Experience(sessions));
        watch.Stop();

        output.WriteLine($"Estadísticas de inicio con 10.000 sesiones: {watch.Elapsed.TotalMilliseconds:F0} ms");
        Assert.True(watch.Elapsed.TotalSeconds < 2);
    }

    [Fact]
    public async Task Sync_Merge_TwoCopiesOf10000Sessions()
    {
        var (journal, storage) = await BuildJournal(10_000);
        var remote = Journal.Parse(storage.LastValue)!;
        foreach (var s in remote.Sessions.Take(1_000))
        {
            s.Notes = "Editada en el otro dispositivo";
            s.UpdatedAt = DateTimeOffset.UtcNow.AddMinutes(1);
        }

        var watch = Stopwatch.StartNew();
        var merged = Cloud.Merge(journal.Data, remote);
        watch.Stop();

        output.WriteLine($"Fusión de 2 copias de 10.000 sesiones: {watch.Elapsed.TotalMilliseconds:F0} ms. Cada sincronización sube {storage.LastValue.Length / 1_000_000.0:F1} M caracteres");
        Assert.Equal(10_000, merged.Sessions.Count);
        Assert.Equal(1_000, merged.Sessions.Count(s => s.Notes == "Editada en el otro dispositivo"));
    }

    // ---------- Datos de prueba ----------

    /// <summary>Diario con las técnicas de ejemplo y N sesiones repartidas en los últimos años.</summary>
    private static async Task<(Journal Journal, FakeLocalStorage Storage)> BuildJournal(int sessions)
    {
        var data = new AppData();
        SeedData.Apply(data);
        var probe = new Journal();
        await probe.ReplaceAsync(data);
        for (var i = 0; i < sessions; i++)
        {
            data.Sessions.Add(FakeSession(probe, Today.AddDays(-i / 3), i));
        }

        // El diario guarda en un localStorage falso para medir el JSON real que se escribiría en el navegador.
        var storage = new FakeLocalStorage();
        var journal = new Journal(storage);
        await journal.ReplaceAsync(data);
        return (journal, storage);
    }

    /// <summary>Sesión típica: 3 técnicas y 4 rolls con 2 eventos cada uno.</summary>
    private static TrainingSession FakeSession(Journal journal, DateOnly day, int seed)
    {
        var techniques = journal.Data.Techniques;
        Guid T(int n) => techniques[(seed + n) % techniques.Count].Id;
        return new TrainingSession
        {
            StartedAt = new DateTimeOffset(day.ToDateTime(new TimeOnly(19, 0))),
            DurationMinutes = 90,
            Satisfaction = 1 + (seed % 5),
            Notes = "Trabajamos guardia cerrada y salidas de montada.",
            UpdatedAt = DateTimeOffset.UtcNow,
            Techniques = [new() { TechniqueId = T(0) }, new() { TechniqueId = T(1) }, new() { TechniqueId = T(2) }],
            Rolls = [.. Enumerable.Range(0, 4).Select(r => new Roll
            {
                PartnerName = $"Compañero {r}",
                PartnerBelt = (Belt)(r % 5),
                Events = [new() { Kind = (RollEventKind)(r % 5), TechniqueId = T(r) }, new() { Kind = RollEventKind.Escape }],
            })],
        };
    }

    /// <summary>Sustituye al navegador: guarda lo que la app escribiría en localStorage.</summary>
    private sealed class FakeLocalStorage : IJSRuntime
    {
        public string LastValue { get; private set; } = "";

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
        {
            if (identifier == "bjjStorage.set" && args is [_, string value])
            {
                LastValue = value;
            }

            return ValueTask.FromResult(default(TValue)!);
        }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) =>
            InvokeAsync<TValue>(identifier, args);
    }
}
