using BjjJournal.Application.Abstractions;
using BjjJournal.Application.Persistence;
using BjjJournal.Application.Sync;
using BjjJournal.Domain.Common;

namespace BjjJournal.Application.Services;

/// <summary>Sincroniza el diario local con la copia en la nube del usuario: pull, fusión y push.</summary>
public sealed class SyncService(JournalContext context, IRemoteJournalStore remote, AuthService auth, TimeProvider time)
{
    private readonly SemaphoreSlim _gate = new(1, 1);

    public DateTimeOffset? LastSyncAt { get; private set; }

    public string? LastError { get; private set; }

    public bool IsSyncing { get; private set; }

    public event Action? StatusChanged;

    public async Task<Result> SyncAsync(CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            var session = await auth.GetValidSessionAsync(ct);
            if (session is null)
            {
                return Result.Ok();
            }

            SetStatus(syncing: true, error: null);
            var local = await context.GetAsync(ct);
            var remoteJson = await remote.PullAsync(session, ct);
            var merged = local;
            if (!string.IsNullOrWhiteSpace(remoteJson) && AppDataSerializer.Deserialize(remoteJson) is { } remoteData)
            {
                merged = JournalMerger.Merge(local, remoteData);
                await context.ReplaceAsync(merged, notify: false, ct);
            }

            await remote.PushAsync(session, AppDataSerializer.Serialize(merged), ct);
            LastSyncAt = time.GetUtcNow();
            SetStatus(syncing: false, error: null);
            return Result.Ok();
        }
        catch (HttpRequestException)
        {
            SetStatus(syncing: false, error: "Sin conexión. Se sincronizará más tarde.");
            return Result.Fail(LastError!);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Al cerrar sesión se borra la copia local: los datos siguen en la cuenta.</summary>
    public Task ClearLocalAsync(CancellationToken ct = default) => context.ReplaceAsync(new AppData(), notify: false, ct);

    private void SetStatus(bool syncing, string? error)
    {
        IsSyncing = syncing;
        LastError = error;
        StatusChanged?.Invoke();
    }
}
