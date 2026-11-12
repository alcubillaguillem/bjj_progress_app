using System.Text.Json;
using BjjJournal.Application.Abstractions;
using BjjJournal.Application.Auth;
using BjjJournal.Domain.Common;

namespace BjjJournal.Application.Services;

/// <summary>Inicio de sesión con correo o Google, sesión persistente y modo "sin cuenta".</summary>
public sealed class AuthService(IAuthApi api, IKeyValueStore store, TimeProvider time)
{
    public const string SessionKey = "bjj-auth.session";
    public const string GuestKey = "bjj-auth.guest";
    private static readonly TimeSpan RefreshMargin = TimeSpan.FromMinutes(2);

    private AuthSession? _session;
    private bool _initialized;

    public event Action? Changed;

    public bool IsConfigured => api.IsConfigured;

    public AuthSession? Session => _session;

    public bool IsSignedIn => _session is not null;

    /// <summary>
    /// Carga la sesión guardada. Si la URL trae tokens (vuelta de Google) o un error, los procesa.
    /// Devuelve el error del proveedor, si lo hay, para mostrarlo.
    /// </summary>
    public async Task<string?> InitializeAsync(string currentUri, CancellationToken ct = default)
    {
        var fragment = ParseFragment(currentUri);
        if (fragment.TryGetValue("access_token", out var access) && fragment.TryGetValue("refresh_token", out var refresh))
        {
            var claims = JwtClaims.Read(access);
            if (claims is { } c)
            {
                var expiresAt = fragment.TryGetValue("expires_at", out var ea) && long.TryParse(ea, out var secs)
                    ? DateTimeOffset.FromUnixTimeSeconds(secs)
                    : c.ExpiresAt ?? time.GetUtcNow().AddHours(1);
                await SetSessionAsync(new AuthSession(access, refresh, expiresAt, c.UserId, c.Email), ct);
                _initialized = true;
                return null;
            }
        }

        if (!_initialized)
        {
            _initialized = true;
            var json = await store.GetAsync(SessionKey, ct);
            if (!string.IsNullOrEmpty(json))
            {
                try
                {
                    _session = JsonSerializer.Deserialize<AuthSession>(json);
                }
                catch (JsonException)
                {
                    _session = null;
                }
            }

            if (_session is not null)
            {
                await GetValidSessionAsync(ct);
            }
        }

        return fragment.TryGetValue("error_description", out var error) ? error.Replace('+', ' ') : null;
    }

    /// <summary>Sesión con el token vigente (lo renueva si caduca pronto). Null si no hay sesión o no se pudo renovar.</summary>
    public async Task<AuthSession?> GetValidSessionAsync(CancellationToken ct = default)
    {
        if (_session is null)
        {
            return null;
        }

        if (_session.ExpiresAt - time.GetUtcNow() > RefreshMargin)
        {
            return _session;
        }

        Result<AuthSession> refreshed;
        try
        {
            refreshed = await api.RefreshAsync(_session.RefreshToken, ct);
        }
        catch (HttpRequestException)
        {
            // Sin red: seguimos en local con la sesión guardada; se renovará más tarde.
            return null;
        }

        if (refreshed.IsSuccess)
        {
            await SetSessionAsync(refreshed.Value!, ct);
            return _session;
        }

        await SetSessionAsync(null, ct);
        return null;
    }

    public async Task<Result> SignInAsync(string email, string password, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrEmpty(password))
        {
            return Result.Fail("Escribe tu correo y tu contraseña.");
        }

        var result = await api.SignInWithPasswordAsync(email.Trim(), password, ct);
        if (!result.IsSuccess)
        {
            return Result.Fail(result.Error!);
        }

        await SetSessionAsync(result.Value, ct);
        return Result.Ok();
    }

    /// <summary>Crea la cuenta. Devuelve true si ya ha entrado, false si tiene que confirmar el correo.</summary>
    public async Task<Result<bool>> SignUpAsync(string email, string password, string redirectTo, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
        {
            return Result<bool>.Fail("Escribe un correo válido.");
        }

        if (password.Length < 6)
        {
            return Result<bool>.Fail("La contraseña debe tener al menos 6 caracteres.");
        }

        var result = await api.SignUpAsync(email.Trim(), password, redirectTo, ct);
        if (!result.IsSuccess)
        {
            return Result<bool>.Fail(result.Error!);
        }

        if (result.Value is null)
        {
            return Result<bool>.Ok(false);
        }

        await SetSessionAsync(result.Value, ct);
        return Result<bool>.Ok(true);
    }

    public Task<Result> SendPasswordResetAsync(string email, string redirectTo, CancellationToken ct = default) =>
        string.IsNullOrWhiteSpace(email)
            ? Task.FromResult(Result.Fail("Escribe tu correo arriba."))
            : api.SendPasswordResetAsync(email.Trim(), redirectTo, ct);

    public string GetGoogleUrl(string redirectTo) => api.GetOAuthUrl("google", redirectTo);

    public async Task SignOutAsync(CancellationToken ct = default)
    {
        if (_session is { } s)
        {
            try
            {
                await api.SignOutAsync(s.AccessToken, ct);
            }
            catch (HttpRequestException)
            {
                // Sin red: la sesión local se borra igualmente.
            }
        }

        await SetSessionAsync(null, ct);
    }

    public async Task<bool> IsGuestAsync(CancellationToken ct = default) =>
        await store.GetAsync(GuestKey, ct) == "1";

    public Task SetGuestAsync(bool guest, CancellationToken ct = default) =>
        store.SetAsync(GuestKey, guest ? "1" : null, ct);

    private async Task SetSessionAsync(AuthSession? session, CancellationToken ct)
    {
        _session = session;
        await store.SetAsync(SessionKey, session is null ? null : JsonSerializer.Serialize(session), ct);
        if (session is not null)
        {
            await store.SetAsync(GuestKey, null, ct);
        }

        Changed?.Invoke();
    }

    internal static Dictionary<string, string> ParseFragment(string uri)
    {
        var index = uri.IndexOf('#');
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        if (index < 0)
        {
            return result;
        }

        foreach (var pair in uri[(index + 1)..].Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var eq = pair.IndexOf('=');
            if (eq > 0)
            {
                result[Uri.UnescapeDataString(pair[..eq])] = Uri.UnescapeDataString(pair[(eq + 1)..]);
            }
        }

        return result;
    }
}
