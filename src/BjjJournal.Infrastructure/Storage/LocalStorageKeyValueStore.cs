using BjjJournal.Application.Abstractions;
using Microsoft.JSInterop;

namespace BjjJournal.Infrastructure.Storage;

public sealed class LocalStorageKeyValueStore(IJSRuntime js) : IKeyValueStore
{
    public async Task<string?> GetAsync(string key, CancellationToken ct = default) =>
        await js.InvokeAsync<string?>("bjjStorage.get", ct, key);

    public async Task SetAsync(string key, string? value, CancellationToken ct = default) =>
        await js.InvokeVoidAsync("bjjStorage.set", ct, key, value);
}
