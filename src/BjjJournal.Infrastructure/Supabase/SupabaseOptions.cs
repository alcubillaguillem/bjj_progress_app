namespace BjjJournal.Infrastructure.Supabase;

/// <summary>URL y clave pública del proyecto (wwwroot/appsettings.json). La clave publicable puede ir en el cliente.</summary>
public sealed class SupabaseOptions
{
    public string Url { get; set; } = "";
    public string PublishableKey { get; set; } = "";

    public bool IsConfigured => !string.IsNullOrWhiteSpace(Url) && !string.IsNullOrWhiteSpace(PublishableKey);
}
