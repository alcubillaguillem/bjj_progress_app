using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using BjjJournal.Models;
using Microsoft.JSInterop;

namespace BjjJournal.Services;

/// <summary>Sesión iniciada en Supabase: tokens y datos del usuario.</summary>
public record AuthSession(string AccessToken, string RefreshToken, DateTimeOffset ExpiresAt, Guid UserId, string? Email);

/// <summary>
/// Cuenta y nube con Supabase, por HTTP: entrar con correo o Google, mantener la sesión
/// y sincronizar el diario (tabla public.bjj_journals, un JSON por usuario).
/// Sin cuenta, la app funciona igual solo en el dispositivo.
/// </summary>
public class Cloud
{
    private const string SessionKey = "bjj-auth.session";
    private const string GuestKey = "bjj-auth.guest";

    private readonly HttpClient _http;
    private readonly IJSRuntime _js;
    private readonly Journal _journal;
    private readonly string _url;
    private readonly string _key;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private CancellationTokenSource? _pendingSync;

    public Cloud(HttpClient http, IJSRuntime js, Journal journal, IConfiguration config)
    {
        _http = http;
        _js = js;
        _journal = journal;
        _url = config["Supabase:Url"]?.TrimEnd('/') ?? "";
        _key = config["Supabase:PublishableKey"] ?? "";
        _journal.Changed += SyncSoon;
    }

    /// <summary>Se lanza cuando cambia la sesión o el estado de la sincronización.</summary>
    public event Action? Changed;

    public bool IsConfigured => _url.Length > 0 && _key.Length > 0;

    public AuthSession? Session { get; private set; }

    public bool IsSignedIn => Session is not null;

    public bool IsSyncing { get; private set; }

    public string? SyncError { get; private set; }

    public DateTimeOffset? LastSyncAt { get; private set; }

    // ---------- Sesión ----------

    /// <summary>
    /// Al arrancar: recupera la sesión guardada o la que trae la URL al volver de Google.
    /// Devuelve el error que manda Supabase en la URL, si lo hay.
    /// </summary>
    public async Task<string?> InitAsync(string uri)
    {
        var fragment = ParseFragment(uri);
        if (fragment.TryGetValue("access_token", out var access) && fragment.TryGetValue("refresh_token", out var refresh)
            && ReadJwt(access) is { } jwt)
        {
            await SetSessionAsync(new AuthSession(access, refresh, jwt.ExpiresAt, jwt.UserId, jwt.Email));
            return null;
        }

        try
        {
            var saved = await _js.InvokeAsync<string?>("bjjStorage.get", SessionKey);
            Session = string.IsNullOrEmpty(saved) ? null : JsonSerializer.Deserialize<AuthSession>(saved);
        }
        catch (JsonException)
        {
            Session = null;
        }

        await ValidSessionAsync();
        return fragment.GetValueOrDefault("error_description")?.Replace('+', ' ');
    }

    /// <summary>La sesión con el token en vigor (lo renueva si caduca en menos de 2 minutos).</summary>
    public async Task<AuthSession?> ValidSessionAsync()
    {
        if (Session is null || Session.ExpiresAt - DateTimeOffset.UtcNow > TimeSpan.FromMinutes(2))
        {
            return Session;
        }

        try
        {
            var (session, _) = await TokenAsync("refresh_token", new { refresh_token = Session.RefreshToken });
            await SetSessionAsync(session);
            return session;
        }
        catch (HttpRequestException)
        {
            return null; // Sin red: seguimos en local y se renovará más tarde.
        }
    }

    public async Task<string?> SignInAsync(string email, string password)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrEmpty(password))
        {
            return "Escribe tu correo y tu contraseña.";
        }

        var (session, error) = await TokenAsync("password", new { email = email.Trim(), password });
        if (session is not null)
        {
            await SetSessionAsync(session);
        }

        return error;
    }

    /// <summary>Crea la cuenta. Si Supabase pide confirmar el correo, no hay sesión todavía (NeedsConfirmation).</summary>
    public async Task<(string? Error, bool NeedsConfirmation)> SignUpAsync(string email, string password, string redirectTo)
    {
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
        {
            return ("Escribe un correo válido.", false);
        }

        if (password.Length < 6)
        {
            return ("La contraseña debe tener al menos 6 caracteres.", false);
        }

        using var response = await SendAsync(HttpMethod.Post, $"/auth/v1/signup?redirect_to={Uri.EscapeDataString(redirectTo)}", new { email = email.Trim(), password });
        if (!response.IsSuccessStatusCode)
        {
            return (await ErrorAsync(response), false);
        }

        var session = ToSession(await response.Content.ReadFromJsonAsync<JsonElement>());
        if (session is null)
        {
            return (null, true);
        }

        await SetSessionAsync(session);
        return (null, false);
    }

    public async Task<string?> SendPasswordResetAsync(string email, string redirectTo)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return "Escribe tu correo arriba.";
        }

        using var response = await SendAsync(HttpMethod.Post, $"/auth/v1/recover?redirect_to={Uri.EscapeDataString(redirectTo)}", new { email = email.Trim() });
        return response.IsSuccessStatusCode ? null : await ErrorAsync(response);
    }

    public string GoogleUrl(string redirectTo) =>
        $"{_url}/auth/v1/authorize?provider=google&redirect_to={Uri.EscapeDataString(redirectTo)}";

    /// <summary>Sube los últimos cambios, cierra la sesión y borra la copia local (los datos siguen en la cuenta).</summary>
    public async Task SignOutAsync()
    {
        await SyncAsync();
        try
        {
            using var _ = await SendAsync(HttpMethod.Post, "/auth/v1/logout", null);
        }
        catch (HttpRequestException)
        {
            // Sin red: la sesión local se borra igualmente.
        }

        await SetSessionAsync(null);
        await _journal.ReplaceAsync(new AppData(), notify: false);
    }

    public async Task<bool> IsGuestAsync() => await _js.InvokeAsync<string?>("bjjStorage.get", GuestKey) == "1";

    public async Task SetGuestAsync() => await _js.InvokeVoidAsync("bjjStorage.set", GuestKey, "1");

    // ---------- Sincronización ----------

    /// <summary>Baja la copia de la nube, la fusiona con la local y sube el resultado.</summary>
    public async Task<string?> SyncAsync()
    {
        await _gate.WaitAsync();
        try
        {
            if (await ValidSessionAsync() is null)
            {
                return null;
            }

            SetStatus(syncing: true, error: null);
            using var pull = await SendAsync(HttpMethod.Get, $"/rest/v1/bjj_journals?select=data&user_id=eq.{Session!.UserId}", null);
            pull.EnsureSuccessStatusCode();
            var rows = await pull.Content.ReadFromJsonAsync<JsonElement>();
            if (rows.GetArrayLength() > 0 && Journal.Parse(rows[0].GetProperty("data").GetRawText()) is { } remote)
            {
                await _journal.ReplaceAsync(Merge(_journal.Data, remote), notify: false);
            }

            var json = JsonSerializer.Serialize(_journal.Data, Journal.JsonOptions);
            using var push = await SendAsync(HttpMethod.Post, "/rest/v1/bjj_journals?on_conflict=user_id",
                $"{{\"user_id\":\"{Session.UserId}\",\"data\":{json},\"updated_at\":\"{DateTimeOffset.UtcNow:O}\"}}");
            push.EnsureSuccessStatusCode();

            LastSyncAt = DateTimeOffset.UtcNow;
            SetStatus(syncing: false, error: null);
            return null;
        }
        catch (HttpRequestException)
        {
            SetStatus(syncing: false, error: "Sin conexión. Se sincronizará más tarde.");
            return SyncError;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Fusiona dos copias del diario elemento a elemento: gana el cambio más reciente (UpdatedAt).</summary>
    public static AppData Merge(AppData local, AppData remote) => new()
    {
        Profile = Newest(local.Profile, remote.Profile),
        Positions = MergeLists(local.Positions, remote.Positions),
        Techniques = MergeLists(local.Techniques, remote.Techniques),
        Sessions = MergeLists(local.Sessions, remote.Sessions),
    };

    private static T? Newest<T>(T? a, T? b)
        where T : Entity => a is null || (b is not null && b.UpdatedAt > a.UpdatedAt) ? b : a;

    private static List<T> MergeLists<T>(List<T> local, List<T> remote)
        where T : Entity =>
        local.Concat(remote).GroupBy(x => x.Id).Select(g => g.Aggregate((a, b) => Newest(a, b)!)).ToList();

    /// <summary>Tras cada cambio local, espera un momento y sincroniza (si hay sesión).</summary>
    private async void SyncSoon()
    {
        if (!IsSignedIn)
        {
            return;
        }

        _pendingSync?.Cancel();
        _pendingSync = new CancellationTokenSource();
        try
        {
            await Task.Delay(1500, _pendingSync.Token);
            await SyncAsync();
        }
        catch (OperationCanceledException)
        {
        }
    }

    // ---------- HTTP con Supabase ----------

    private async Task SetSessionAsync(AuthSession? session)
    {
        Session = session;
        await _js.InvokeVoidAsync("bjjStorage.set", SessionKey, session is null ? null : JsonSerializer.Serialize(session));
        if (session is not null)
        {
            await _js.InvokeVoidAsync("bjjStorage.set", GuestKey, null);
        }

        Changed?.Invoke();
    }

    private void SetStatus(bool syncing, string? error)
    {
        IsSyncing = syncing;
        SyncError = error;
        Changed?.Invoke();
    }

    private async Task<(AuthSession? Session, string? Error)> TokenAsync(string grant, object body)
    {
        using var response = await SendAsync(HttpMethod.Post, $"/auth/v1/token?grant_type={grant}", body);
        if (!response.IsSuccessStatusCode)
        {
            return (null, await ErrorAsync(response));
        }

        var session = ToSession(await response.Content.ReadFromJsonAsync<JsonElement>());
        return session is null ? (null, "Respuesta inesperada del servidor.") : (session, null);
    }

    /// <summary>Petición a Supabase con la clave pública y, si hay sesión, el token del usuario.</summary>
    private Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, object? body)
    {
        var request = new HttpRequestMessage(method, _url + path);
        request.Headers.Add("apikey", _key);
        if (Session is not null && !path.StartsWith("/auth/v1/token"))
        {
            request.Headers.Authorization = new("Bearer", Session.AccessToken);
        }

        if (body is string json)
        {
            request.Headers.Add("Prefer", "resolution=merge-duplicates,return=minimal");
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        }
        else if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return _http.SendAsync(request);
    }

    private static AuthSession? ToSession(JsonElement body)
    {
        if (!body.TryGetProperty("access_token", out var access) || ReadJwt(access.GetString()!) is not { } jwt)
        {
            return null; // p. ej. al registrarse con confirmación de correo: llega el usuario sin tokens
        }

        return new AuthSession(access.GetString()!, body.GetProperty("refresh_token").GetString()!, jwt.ExpiresAt, jwt.UserId, jwt.Email);
    }

    /// <summary>Lee (sin validar la firma, eso lo hace el servidor) el usuario, el correo y la caducidad del token.</summary>
    public static (Guid UserId, string? Email, DateTimeOffset ExpiresAt)? ReadJwt(string jwt)
    {
        try
        {
            var payload = jwt.Split('.')[1].Replace('-', '+').Replace('_', '/');
            var json = JsonDocument.Parse(Convert.FromBase64String(payload.PadRight(payload.Length + ((4 - (payload.Length % 4)) % 4), '='))).RootElement;
            var email = json.TryGetProperty("email", out var e) ? e.GetString() : null;
            return (Guid.Parse(json.GetProperty("sub").GetString()!),
                    string.IsNullOrEmpty(email) ? null : email,
                    json.TryGetProperty("exp", out var exp) ? DateTimeOffset.FromUnixTimeSeconds(exp.GetInt64()) : DateTimeOffset.UtcNow.AddHours(1));
        }
        catch (Exception ex) when (ex is FormatException or JsonException or IndexOutOfRangeException or KeyNotFoundException or InvalidOperationException or ArgumentNullException)
        {
            return null;
        }
    }

    /// <summary>Parámetros tras '#' en la URL (así devuelve Supabase los tokens al volver de Google).</summary>
    public static Dictionary<string, string> ParseFragment(string uri) =>
        uri.Contains('#')
            ? uri[(uri.IndexOf('#') + 1)..].Split('&').Select(p => p.Split('=', 2)).Where(p => p.Length == 2).DistinctBy(p => p[0])
                .ToDictionary(p => Uri.UnescapeDataString(p[0]), p => Uri.UnescapeDataString(p[1]))
            : [];

    /// <summary>Traduce los errores más comunes de Supabase.</summary>
    private static async Task<string> ErrorAsync(HttpResponseMessage response)
    {
        string? code = null, message = null;
        try
        {
            var root = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
            code = (root.TryGetProperty("error_code", out var c) ? c : root.TryGetProperty("error", out c) ? c : default).ToString();
            message = (root.TryGetProperty("msg", out var m) ? m : root.TryGetProperty("error_description", out m) ? m : root.TryGetProperty("message", out m) ? m : default).ToString();
        }
        catch (JsonException)
        {
        }

        return code switch
        {
            "invalid_credentials" or "invalid_grant" => "Correo o contraseña incorrectos.",
            "email_not_confirmed" => "Confirma tu correo: te enviamos un enlace al registrarte.",
            "user_already_exists" => "Ya hay una cuenta con ese correo. Inicia sesión.",
            "weak_password" => "La contraseña es demasiado débil.",
            "over_email_send_rate_limit" or "over_request_rate_limit" => "Demasiados intentos. Espera un minuto.",
            "refresh_token_not_found" or "refresh_token_already_used" => "La sesión ha caducado. Vuelve a entrar.",
            _ => string.IsNullOrEmpty(message) ? $"Error del servidor ({(int)response.StatusCode})." : message,
        };
    }
}
