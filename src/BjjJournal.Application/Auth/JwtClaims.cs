using System.Text;
using System.Text.Json;

namespace BjjJournal.Application.Auth;

/// <summary>Lee (sin validar) las claims "sub", "email" y "exp" de un JWT. La firma la valida el servidor.</summary>
public static class JwtClaims
{
    public static (Guid UserId, string? Email, DateTimeOffset? ExpiresAt)? Read(string jwt)
    {
        var parts = jwt.Split('.');
        if (parts.Length < 2)
        {
            return null;
        }

        try
        {
            var payload = parts[1].Replace('-', '+').Replace('_', '/');
            payload = payload.PadRight(payload.Length + ((4 - (payload.Length % 4)) % 4), '=');
            using var doc = JsonDocument.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(payload)));
            var root = doc.RootElement;
            if (!root.TryGetProperty("sub", out var sub) || !Guid.TryParse(sub.GetString(), out var id))
            {
                return null;
            }

            var email = root.TryGetProperty("email", out var e) ? e.GetString() : null;
            DateTimeOffset? exp = root.TryGetProperty("exp", out var x) && x.TryGetInt64(out var secs)
                ? DateTimeOffset.FromUnixTimeSeconds(secs)
                : null;
            return (id, string.IsNullOrEmpty(email) ? null : email, exp);
        }
        catch (Exception ex) when (ex is FormatException or JsonException)
        {
            return null;
        }
    }
}
