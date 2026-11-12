using BjjJournal.Application.Auth;

namespace BjjJournal.Application.Abstractions;

/// <summary>Copia del diario en la nube, una por usuario.</summary>
public interface IRemoteJournalStore
{
    /// <summary>JSON del diario del usuario, o null si aún no tiene copia en la nube.</summary>
    Task<string?> PullAsync(AuthSession session, CancellationToken ct = default);

    Task PushAsync(AuthSession session, string json, CancellationToken ct = default);
}
