using BjjJournal.Application.Auth;
using BjjJournal.Domain.Common;

namespace BjjJournal.Application.Abstractions;

/// <summary>Proveedor de identidad (Supabase Auth en Infrastructure).</summary>
public interface IAuthApi
{
    /// <summary>False si la app no tiene configurado el proveedor (modo solo local).</summary>
    bool IsConfigured { get; }

    Task<Result<AuthSession>> SignInWithPasswordAsync(string email, string password, CancellationToken ct = default);

    /// <summary>Devuelve la sesión, o null si hay que confirmar el correo antes de entrar.</summary>
    Task<Result<AuthSession?>> SignUpAsync(string email, string password, string redirectTo, CancellationToken ct = default);

    Task<Result<AuthSession>> RefreshAsync(string refreshToken, CancellationToken ct = default);

    Task SignOutAsync(string accessToken, CancellationToken ct = default);

    Task<Result> SendPasswordResetAsync(string email, string redirectTo, CancellationToken ct = default);

    /// <summary>URL a la que se navega para entrar con un proveedor externo (google).</summary>
    string GetOAuthUrl(string provider, string redirectTo);
}
