using System.Text;
using BjjJournal.Application.Abstractions;
using BjjJournal.Application.Auth;
using BjjJournal.Domain.Common;

namespace BjjJournal.Application.Tests;

internal static class Jwt
{
    public static string Make(Guid sub, string email, DateTimeOffset exp)
    {
        static string B64(string s) => Convert.ToBase64String(Encoding.UTF8.GetBytes(s)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        return $"{B64("{\"alg\":\"HS256\"}")}.{B64($"{{\"sub\":\"{sub}\",\"email\":\"{email}\",\"exp\":{exp.ToUnixTimeSeconds()}}}")}.firma";
    }
}

internal sealed class FakeAuthApi(TimeProvider time) : IAuthApi
{
    public Guid UserId { get; } = Guid.NewGuid();
    public string Password { get; set; } = "secreto1";
    public bool RequireEmailConfirmation { get; set; }
    public bool FailRefresh { get; set; }
    public int Refreshes { get; private set; }

    public bool IsConfigured => true;

    public AuthSession NewSession(string email = "ana@test.com") =>
        new(Jwt.Make(UserId, email, time.GetUtcNow().AddHours(1)), "refresh-" + Guid.NewGuid(), time.GetUtcNow().AddHours(1), UserId, email);

    public Task<Result<AuthSession>> SignInWithPasswordAsync(string email, string password, CancellationToken ct = default) =>
        Task.FromResult(password == Password ? Result<AuthSession>.Ok(NewSession(email)) : Result<AuthSession>.Fail("Correo o contraseña incorrectos."));

    public Task<Result<AuthSession?>> SignUpAsync(string email, string password, string redirectTo, CancellationToken ct = default) =>
        Task.FromResult(Result<AuthSession?>.Ok(RequireEmailConfirmation ? null : NewSession(email)));

    public Task<Result<AuthSession>> RefreshAsync(string refreshToken, CancellationToken ct = default)
    {
        Refreshes++;
        return Task.FromResult(FailRefresh ? Result<AuthSession>.Fail("La sesión ha caducado.") : Result<AuthSession>.Ok(NewSession()));
    }

    public Task SignOutAsync(string accessToken, CancellationToken ct = default) => Task.CompletedTask;

    public Task<Result> SendPasswordResetAsync(string email, string redirectTo, CancellationToken ct = default) => Task.FromResult(Result.Ok());

    public string GetOAuthUrl(string provider, string redirectTo) => $"https://auth.test/authorize?provider={provider}&redirect_to={Uri.EscapeDataString(redirectTo)}";
}

internal sealed class FakeKeyValueStore : IKeyValueStore
{
    public Dictionary<string, string> Values { get; } = [];

    public Task<string?> GetAsync(string key, CancellationToken ct = default) =>
        Task.FromResult(Values.TryGetValue(key, out var v) ? v : null);

    public Task SetAsync(string key, string? value, CancellationToken ct = default)
    {
        if (value is null)
        {
            Values.Remove(key);
        }
        else
        {
            Values[key] = value;
        }

        return Task.CompletedTask;
    }
}

internal sealed class FakeRemoteStore : IRemoteJournalStore
{
    public string? Json { get; set; }
    public int Pushes { get; private set; }
    public bool Offline { get; set; }

    public Task<string?> PullAsync(AuthSession session, CancellationToken ct = default) =>
        Offline ? throw new HttpRequestException("sin red") : Task.FromResult(Json);

    public Task PushAsync(AuthSession session, string json, CancellationToken ct = default)
    {
        if (Offline)
        {
            throw new HttpRequestException("sin red");
        }

        Json = json;
        Pushes++;
        return Task.CompletedTask;
    }
}
