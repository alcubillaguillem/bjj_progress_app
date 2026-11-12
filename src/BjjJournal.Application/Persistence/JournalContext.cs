using BjjJournal.Application.Abstractions;

namespace BjjJournal.Application.Persistence;

/// <summary>Unidad de trabajo: carga el diario una vez, lo mantiene en memoria y lo guarda tras cada cambio.</summary>
public sealed class JournalContext(IAppDataStore store)
{
    private AppData? _data;

    /// <summary>Se lanza después de cada guardado, para refrescar la UI.</summary>
    public event Action? Changed;

    public async Task<AppData> GetAsync(CancellationToken ct = default)
    {
        if (_data is not null)
        {
            return _data;
        }

        var json = await store.ReadAsync(ct);
        _data = string.IsNullOrWhiteSpace(json) ? new AppData() : AppDataSerializer.Deserialize(json) ?? new AppData();
        return _data;
    }

    public async Task SaveAsync(CancellationToken ct = default)
    {
        var data = await GetAsync(ct);
        await store.WriteAsync(AppDataSerializer.Serialize(data), ct);
        Changed?.Invoke();
    }

    public async Task ReplaceAsync(AppData data, CancellationToken ct = default)
    {
        _data = data;
        await SaveAsync(ct);
    }
}
