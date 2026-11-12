using System.Text.Json;
using System.Text.Json.Serialization;

namespace BjjJournal.Application.Persistence;

/// <summary>JSON versionado (schemaVersion), con enums como texto para que el fichero sea legible.</summary>
public static class AppDataSerializer
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false,
        Converters = { new JsonStringEnumConverter() },
    };

    private static readonly JsonSerializerOptions IndentedOptions = new(Options) { WriteIndented = true };

    public static string Serialize(AppData data, bool indented = false) =>
        JsonSerializer.Serialize(data, indented ? IndentedOptions : Options);

    public static AppData? Deserialize(string json) => JsonSerializer.Deserialize<AppData>(json, Options);
}
