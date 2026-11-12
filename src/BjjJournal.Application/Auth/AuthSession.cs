namespace BjjJournal.Application.Auth;

/// <summary>Sesión iniciada: tokens del proveedor (Supabase Auth) y datos básicos del usuario.</summary>
public sealed record AuthSession(
    string AccessToken,
    string RefreshToken,
    DateTimeOffset ExpiresAt,
    Guid UserId,
    string? Email);
