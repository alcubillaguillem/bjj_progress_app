using BjjJournal.Application.Abstractions;
using Microsoft.JSInterop;

namespace BjjJournal.Infrastructure.Storage;

/// <summary>Guarda el diario en el localStorage del navegador (offline-first: los datos viven en el dispositivo).</summary>
public sealed class LocalStorageDataStore(IJSRuntime js) : IAppDataStore
{
    public const string Key = "bjj-journal.v1";

    public async Task<string?> ReadAsync(CancellationToken ct = default) =>
        await js.InvokeAsync<string?>("bjjStorage.get", ct, Key);

    public async Task WriteAsync(string json, CancellationToken ct = default) =>
        await js.InvokeVoidAsync("bjjStorage.set", ct, Key, json);
}
