namespace BjjJournal.Application.Abstractions;

/// <summary>Almacén físico del diario. Infrastructure lo implementa (localStorage en el navegador).</summary>
public interface IAppDataStore
{
    Task<string?> ReadAsync(CancellationToken ct = default);

    Task WriteAsync(string json, CancellationToken ct = default);
}
