using System.Text;
using System.Text.Json;
using BjjJournal.Application.Abstractions;
using BjjJournal.Application.Auth;

namespace BjjJournal.Infrastructure.Supabase;

/// <summary>Tabla public.bjj_journals vía PostgREST. La seguridad por usuario la aplica RLS en el servidor.</summary>
public sealed class SupabaseJournalStore(HttpClient http, SupabaseOptions options) : IRemoteJournalStore
{
    private string Table => options.Url.TrimEnd('/') + "/rest/v1/bjj_journals";

    public async Task<string?> PullAsync(AuthSession session, CancellationToken ct = default)
    {
        using var request = Request(HttpMethod.Get, $"{Table}?select=data&user_id=eq.{session.UserId}", session);
        using var response = await http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        return doc.RootElement.GetArrayLength() == 0 ? null : doc.RootElement[0].GetProperty("data").GetRawText();
    }

    public async Task PushAsync(AuthSession session, string json, CancellationToken ct = default)
    {
        var body = $"{{\"user_id\":\"{session.UserId}\",\"data\":{json},\"updated_at\":\"{DateTimeOffset.UtcNow:O}\"}}";
        using var request = Request(HttpMethod.Post, $"{Table}?on_conflict=user_id", session);
        request.Headers.Add("Prefer", "resolution=merge-duplicates,return=minimal");
        request.Content = new StringContent(body, Encoding.UTF8, "application/json");
        using var response = await http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
    }

    private HttpRequestMessage Request(HttpMethod method, string url, AuthSession session)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Add("apikey", options.PublishableKey);
        request.Headers.Authorization = new("Bearer", session.AccessToken);
        return request;
    }
}
