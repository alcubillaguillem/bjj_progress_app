using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using BjjJournal.Models;
using Microsoft.JSInterop;

namespace BjjJournal.Services;

/// <summary>
/// El diario en memoria. Se carga de localStorage al arrancar y se guarda tras cada cambio.
/// Las páginas leen de aquí y guardan con los métodos Save/Delete, que validan antes de guardar.
/// </summary>
public class Journal(IJSRuntime? js = null)
{
    private const string StorageKey = "bjj-journal.v1";

    /// <summary>JSON con nombres en camelCase y enums como texto, para que el fichero se pueda leer.</summary>
    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public AppData Data { get; private set; } = new();

    /// <summary>Se lanza tras cada guardado (la sincronización lo escucha).</summary>
    public event Action? Changed;

    // ---------- Lectura ----------

    public IEnumerable<TrainingSession> Sessions => Data.Sessions.Where(s => !s.IsDeleted);

    public IEnumerable<Technique> Techniques => Data.Techniques.Where(t => !t.IsDeleted);

    public IEnumerable<Position> Positions =>
        Data.Positions.Where(p => !p.IsDeleted).OrderBy(p => p.Name, StringComparer.CurrentCultureIgnoreCase);

    public TrainingSession? Session(Guid id) => Sessions.FirstOrDefault(s => s.Id == id);

    public Technique? Technique(Guid id) => Techniques.FirstOrDefault(t => t.Id == id);

    /// <summary>Incluye técnicas borradas: el histórico de sesiones se conserva.</summary>
    public string? TechniqueName(Guid? id) => Data.Techniques.FirstOrDefault(t => t.Id == id)?.Name;

    public string? PositionName(Guid? id) => Data.Positions.FirstOrDefault(p => p.Id == id)?.Name;

    /// <summary>Veces trabajada (sesiones distintas) y última fecha, por técnica.</summary>
    public Dictionary<Guid, (int Count, DateTimeOffset Last)> Usage()
    {
        var usage = new Dictionary<Guid, (int Count, DateTimeOffset Last)>();
        foreach (var s in Sessions)
        {
            foreach (var id in s.TechniqueIds())
            {
                usage[id] = usage.TryGetValue(id, out var u)
                    ? (u.Count + 1, u.Last > s.StartedAt ? u.Last : s.StartedAt)
                    : (1, s.StartedAt);
            }
        }

        return usage;
    }

    /// <summary>Búsqueda sin tildes ni mayúsculas por nombre, posición, etiqueta, notas o descripción.</summary>
    public IEnumerable<Technique> Search(string? query)
    {
        var q = Normalize(query);
        return Techniques.Where(t => q.Length == 0
            || new[] { t.Name, PositionName(t.PositionId), t.Notes, t.Description }.Concat(t.Tags).Any(x => Normalize(x).Contains(q)));
    }

    // ---------- Cambios ----------

    public async Task LoadAsync() => Data = Parse(await js!.InvokeAsync<string?>("bjjStorage.get", StorageKey)) ?? new AppData();

    public async Task SaveAsync(bool notify = true)
    {
        if (js is not null)
        {
            await js.InvokeVoidAsync("bjjStorage.set", StorageKey, JsonSerializer.Serialize(Data, JsonOptions));
        }

        if (notify)
        {
            Changed?.Invoke();
        }
    }

    public Task ReplaceAsync(AppData data, bool notify = true)
    {
        Data = data;
        return SaveAsync(notify);
    }

    /// <summary>Guarda el perfil. Al terminar la bienvenida puede cargar las técnicas de ejemplo.</summary>
    public async Task<string?> SaveProfileAsync(UserProfile profile, bool loadSeed = false)
    {
        if (string.IsNullOrWhiteSpace(profile.DisplayName))
        {
            return "El nombre es obligatorio.";
        }

        if (profile.Stripes is < 0 or > 4)
        {
            return "Los grados van de 0 a 4.";
        }

        if (loadSeed && Data.Techniques.Count == 0)
        {
            SeedData.Apply(Data);
        }

        profile.DisplayName = profile.DisplayName.Trim();
        profile.GymName = Clean(profile.GymName);
        profile.UpdatedAt = DateTimeOffset.UtcNow;
        Data.Profile = profile;
        await SaveAsync();
        return null;
    }

    /// <summary>Crea o actualiza una técnica. Devuelve el error, o null si se ha guardado.</summary>
    public async Task<string?> SaveTechniqueAsync(Technique t)
    {
        if (string.IsNullOrWhiteSpace(t.Name))
        {
            return "El nombre de la técnica es obligatorio.";
        }

        t.Name = t.Name.Trim();
        if (Techniques.Any(x => x.Id != t.Id && x.PositionId == t.PositionId && Normalize(x.Name) == Normalize(t.Name)))
        {
            return "Ya tienes una técnica con ese nombre en esa posición.";
        }

        t.Description = Clean(t.Description);
        t.Notes = Clean(t.Notes);
        t.Tags = CleanList(t.Tags);
        t.Links = CleanList(t.Links);
        Upsert(Data.Techniques, t);
        await SaveAsync();
        return null;
    }

    /// <summary>Crea o actualiza una sesión. Devuelve el error, o null si se ha guardado.</summary>
    public async Task<string?> SaveSessionAsync(TrainingSession s)
    {
        if (s.DurationMinutes <= 0)
        {
            return "La duración debe ser mayor que 0 minutos.";
        }

        if (s.Satisfaction is < 1 or > 5)
        {
            return "La satisfacción va de 1 a 5.";
        }

        if (s.Rolls.Any(r => r.DurationMinutes <= 0))
        {
            return "Cada roll debe durar más de 0 minutos.";
        }

        s.StartedAt = s.StartedAt.ToUniversalTime();
        s.Location = Clean(s.Location);
        s.Notes = Clean(s.Notes);
        s.Techniques = s.Techniques.DistinctBy(t => t.TechniqueId).ToList(); // una técnica solo una vez por sesión
        foreach (var r in s.Rolls)
        {
            r.PartnerName = Clean(r.PartnerName);
        }

        Upsert(Data.Sessions, s);
        await SaveAsync();
        return null;
    }

    /// <summary>Crea una posición, o devuelve la que ya existe con ese nombre.</summary>
    public async Task<Position> AddPositionAsync(string name)
    {
        var existing = Data.Positions.FirstOrDefault(p => !p.IsDeleted && Normalize(p.Name) == Normalize(name));
        if (existing is not null)
        {
            return existing;
        }

        var position = new Position { Name = name.Trim() };
        Upsert(Data.Positions, position);
        await SaveAsync();
        return position;
    }

    /// <summary>Borrado lógico: así el borrado también se sincroniza.</summary>
    public async Task DeleteAsync(Entity item)
    {
        item.IsDeleted = true;
        item.UpdatedAt = DateTimeOffset.UtcNow;
        await SaveAsync();
    }

    /// <summary>Sesión nueva con lo de la última: duración, tipo, gi/no-gi y lugar. Con día, a la hora de la última.</summary>
    public TrainingSession NewSession(DateOnly? day = null)
    {
        var last = Sessions.MaxBy(s => s.StartedAt);
        var now = DateTime.Now;
        var start = new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute, 0);
        if (day is { } d)
        {
            start = d.ToDateTime(last is null ? new TimeOnly(19, 0) : TimeOnly.FromDateTime(last.StartedAt.LocalDateTime));
        }

        return new TrainingSession
        {
            StartedAt = new DateTimeOffset(start, TimeZoneInfo.Local.GetUtcOffset(start)),
            DurationMinutes = last?.DurationMinutes ?? 90,
            Type = last?.Type ?? SessionType.Class,
            Attire = last?.Attire ?? Attire.Gi,
            Location = last?.Location,
        };
    }

    // ---------- Copias de seguridad (JSON) ----------

    public string Export() => JsonSerializer.Serialize(Data, new JsonSerializerOptions(JsonOptions) { WriteIndented = true });

    /// <summary>Lee un fichero de copia. Devuelve el error, o null si es válido.</summary>
    public static string? ReadBackup(string json, out AppData data)
    {
        data = new AppData();
        try
        {
            if (Parse(json) is not { } parsed)
            {
                return "El fichero está vacío.";
            }

            data = parsed;
        }
        catch (JsonException)
        {
            return "El fichero no es un JSON válido de BJJ Journal.";
        }

        return data.SchemaVersion is < 1 or > AppData.CurrentSchemaVersion
            ? $"Versión de fichero no soportada ({data.SchemaVersion})."
            : null;
    }

    /// <summary>Técnicas del fichero que ya existen con otro Id (mismo nombre y posición): Id del fichero → Id actual.</summary>
    public Dictionary<Guid, Guid> DuplicateTechniques(AppData incoming)
    {
        var positionNames = Data.Positions.Concat(incoming.Positions).DistinctBy(p => p.Id).ToDictionary(p => p.Id, p => Normalize(p.Name));
        string Key(Technique t) => Normalize(t.Name) + "|" + (t.PositionId is { } id ? positionNames.GetValueOrDefault(id) : "");

        var existing = Techniques.DistinctBy(Key).ToDictionary(Key, t => t.Id);
        return incoming.Techniques
            .Where(t => !t.IsDeleted && Data.Techniques.All(x => x.Id != t.Id) && existing.ContainsKey(Key(t)))
            .ToDictionary(t => t.Id, t => existing[Key(t)]);
    }

    /// <summary>
    /// Reemplazar: sustituye todo. Fusionar: añade lo nuevo; las técnicas y posiciones repetidas se reutilizan
    /// y las sesiones con el mismo Id se omiten.
    /// </summary>
    public async Task ImportAsync(AppData incoming, bool replace)
    {
        if (replace)
        {
            await ReplaceAsync(incoming);
            return;
        }

        Data.Profile ??= incoming.Profile;

        var map = DuplicateTechniques(incoming);
        foreach (var p in incoming.Positions)
        {
            var same = Data.Positions.FirstOrDefault(x => x.Id == p.Id || (!x.IsDeleted && Normalize(x.Name) == Normalize(p.Name)));
            if (same is null)
            {
                Data.Positions.Add(p);
            }
            else
            {
                map[p.Id] = same.Id;
            }
        }

        Guid? Map(Guid? id) => id is { } v && map.TryGetValue(v, out var m) ? m : id;

        foreach (var t in incoming.Techniques.Where(t => !map.ContainsKey(t.Id) && Data.Techniques.All(x => x.Id != t.Id)))
        {
            t.PositionId = Map(t.PositionId);
            Data.Techniques.Add(t);
        }

        foreach (var s in incoming.Sessions.Where(s => Data.Sessions.All(x => x.Id != s.Id)))
        {
            s.Techniques.ForEach(t => t.TechniqueId = Map(t.TechniqueId)!.Value);
            foreach (var e in s.Events)
            {
                e.TechniqueId = Map(e.TechniqueId);
                e.PositionId = Map(e.PositionId);
            }

            Data.Sessions.Add(s);
        }

        await SaveAsync();
    }

    // ---------- Utilidades ----------

    public static AppData? Parse(string? json) =>
        string.IsNullOrWhiteSpace(json) ? null : JsonSerializer.Deserialize<AppData>(json, JsonOptions);

    /// <summary>Copia independiente de un objeto, para editarlo en un formulario sin tocar el original.</summary>
    public static T Copy<T>(T item) => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(item, JsonOptions), JsonOptions)!;

    /// <summary>Texto sin tildes ni mayúsculas, para comparar ("Kímura" == "kimura").</summary>
    public static string Normalize(string? text)
    {
        var builder = new StringBuilder();
        foreach (var c in (text ?? "").Trim().Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(char.ToLowerInvariant(c));
            }
        }

        return builder.ToString();
    }

    private static string? Clean(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();

    private static List<string> CleanList(IEnumerable<string> values) =>
        values.Select(v => v.Trim().TrimStart('#')).Where(v => v.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

    /// <summary>Sustituye el elemento con el mismo Id o lo añade, marcando la fecha del cambio.</summary>
    private static void Upsert<T>(List<T> list, T item)
        where T : Entity
    {
        item.UpdatedAt = DateTimeOffset.UtcNow;
        var index = list.FindIndex(x => x.Id == item.Id);
        if (index >= 0)
        {
            list[index] = item;
        }
        else
        {
            list.Add(item);
        }
    }
}
