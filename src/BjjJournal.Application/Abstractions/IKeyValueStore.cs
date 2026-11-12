namespace BjjJournal.Application.Abstractions;

/// <summary>Almacén clave-valor del dispositivo (sesión, preferencias).</summary>
public interface IKeyValueStore
{
    Task<string?> GetAsync(string key, CancellationToken ct = default);

    /// <summary>Guarda el valor; null lo borra.</summary>
    Task SetAsync(string key, string? value, CancellationToken ct = default);
}
