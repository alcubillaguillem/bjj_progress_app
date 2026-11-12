using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using BjjJournal.Application.Abstractions;
using BjjJournal.Application.Auth;
using BjjJournal.Domain.Common;

namespace BjjJournal.Infrastructure.Supabase;

/// <summary>Cliente mínimo de Supabase Auth (GoTrue) por HTTP: correo/contraseña, Google y renovación de tokens.</summary>
public sealed class SupabaseAuthApi(HttpClient http, SupabaseOptions options) : IAuthApi
{
    public bool IsConfigured => options.IsConfigured;

    private string Base => options.Url.TrimEnd('/') + "/auth/v1";

    public Task<Result<AuthSession>> SignInWithPasswordAsync(string email, string password, CancellationToken ct = default) =>
        TokenAsync("password", new { email, password }, ct);

    public Task<Result<AuthSession>> RefreshAsync(string refreshToken, CancellationToken ct = default) =>
        TokenAsync("refresh_token", new { refresh_token = refreshToken }, ct);

    public async Task<Result<AuthSession?>> SignUpAsync(string email, string password, string redirectTo, CancellationToken ct = default)
    {
        using var response = await SendAsync(HttpMethod.Post, $"/signup?redirect_to={Uri.EscapeDataString(redirectTo)}", new { email, password }, null, ct);
        if (!response.IsSuccessStatusCode)
        {
            return Result<AuthSession?>.Fail(await ErrorAsync(response, ct));
        }

        var body = await response.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken: ct);
        // Con confirmación de correo activada, Supabase devuelve el usuario sin tokens.
        return Result<AuthSession?>.Ok(body?.AccessToken is null ? null : body.ToSession());
    }

    public async Task SignOutAsync(string accessToken, CancellationToken ct = default)
    {
        using var _ = await SendAsync(HttpMethod.Post, "/logout", null, accessToken, ct);
    }

    public async Task<Result> SendPasswordResetAsync(string email, string redirectTo, CancellationToken ct = default)
    {
        using var response = await SendAsync(HttpMethod.Post, $"/recover?redirect_to={Uri.EscapeDataString(redirectTo)}", new { email }, null, ct);
        return response.IsSuccessStatusCode ? Result.Ok() : Result.Fail(await ErrorAsync(response, ct));
    }

    public string GetOAuthUrl(string provider, string redirectTo) =>
        $"{Base}/authorize?provider={Uri.EscapeDataString(provider)}&redirect_to={Uri.EscapeDataString(redirectTo)}";

    private async Task<Result<AuthSession>> TokenAsync(string grant, object payload, CancellationToken ct)
    {
        using var response = await SendAsync(HttpMethod.Post, $"/token?grant_type={grant}", payload, null, ct);
        if (!response.IsSuccessStatusCode)
        {
            return Result<AuthSession>.Fail(await ErrorAsync(response, ct));
        }

        var body = await response.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken: ct);
        return body?.AccessToken is null
            ? Result<AuthSession>.Fail("Respuesta inesperada del servidor.")
            : Result<AuthSession>.Ok(body.ToSession());
    }

    private Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, object? payload, string? bearer, CancellationToken ct)
    {
        var request = new HttpRequestMessage(method, Base + path);
        request.Headers.Add("apikey", options.PublishableKey);
        if (bearer is not null)
        {
            request.Headers.Authorization = new("Bearer", bearer);
        }

        if (payload is not null)
        {
            request.Content = JsonContent.Create(payload);
        }

        return http.SendAsync(request, ct);
    }

    /// <summary>Traduce los errores más comunes de Supabase Auth.</summary>
    private static async Task<string> ErrorAsync(HttpResponseMessage response, CancellationToken ct)
    {
        string? code = null;
        string? message = null;
        try
        {
            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            var root = doc.RootElement;
            code = root.TryGetProperty("error_code", out var c) ? c.GetString() : root.TryGetProperty("error", out var e) ? e.GetString() : null;
            message = root.TryGetProperty("msg", out var m) ? m.GetString()
                : root.TryGetProperty("error_description", out var d) ? d.GetString()
                : root.TryGetProperty("message", out var mm) ? mm.GetString() : null;
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
            _ => message ?? $"Error del servidor ({(int)response.StatusCode}).",
        };
    }

    private sealed class TokenResponse
    {
        [JsonPropertyName("access_token")] public string? AccessToken { get; set; }
        [JsonPropertyName("refresh_token")] public string? RefreshToken { get; set; }
        [JsonPropertyName("expires_at")] public long? ExpiresAt { get; set; }
        [JsonPropertyName("expires_in")] public int? ExpiresIn { get; set; }
        [JsonPropertyName("user")] public UserDto? User { get; set; }

        public AuthSession ToSession()
        {
            var claims = JwtClaims.Read(AccessToken!);
            var expires = ExpiresAt is { } at
                ? DateTimeOffset.FromUnixTimeSeconds(at)
                : claims?.ExpiresAt ?? DateTimeOffset.UtcNow.AddSeconds(ExpiresIn ?? 3600);
            return new AuthSession(AccessToken!, RefreshToken ?? "", expires, User?.Id ?? claims?.UserId ?? Guid.Empty, User?.Email ?? claims?.Email);
        }
    }

    private sealed class UserDto
    {
        [JsonPropertyName("id")] public Guid Id { get; set; }
        [JsonPropertyName("email")] public string? Email { get; set; }
    }
}
