using BjjJournal.Application.Persistence;
using BjjJournal.Application.Services;

namespace BjjJournal.App.Ui;

/// <summary>Tras cada cambio local, espera un momento y sincroniza con la cuenta (si hay sesión).</summary>
public sealed class SyncCoordinator : IDisposable
{
    private static readonly TimeSpan Delay = TimeSpan.FromSeconds(1.5);
    private readonly JournalContext _context;
    private readonly SyncService _sync;
    private readonly AuthService _auth;
    private CancellationTokenSource? _pending;

    public SyncCoordinator(JournalContext context, SyncService sync, AuthService auth)
    {
        _context = context;
        _sync = sync;
        _auth = auth;
        _context.Changed += OnChanged;
    }

    private async void OnChanged()
    {
        if (!_auth.IsSignedIn)
        {
            return;
        }

        _pending?.Cancel();
        _pending = new CancellationTokenSource();
        var token = _pending.Token;
        try
        {
            await Task.Delay(Delay, token);
            await _sync.SyncAsync(token);
        }
        catch (OperationCanceledException)
        {
        }
    }

    public void Dispose() => _context.Changed -= OnChanged;
}
