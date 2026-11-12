using System.Text.Json;
using BjjJournal.Application.Dtos;
using BjjJournal.Application.Persistence;
using BjjJournal.Domain.Common;
using BjjJournal.Domain.Entities;
using BjjJournal.Domain.Rules;

namespace BjjJournal.Application.Services;

/// <summary>F10 (fase 1): exportar e importar todo el diario en un JSON versionado.</summary>
public sealed class BackupService(JournalContext context, TimeProvider time)
{
    public async Task<string> ExportJsonAsync(CancellationToken ct = default)
    {
        var data = await context.GetAsync(ct);
        data.ExportedAt = time.GetUtcNow();
        var json = AppDataSerializer.Serialize(data, indented: true);
        data.ExportedAt = null;
        return json;
    }

    /// <summary>Lee el fichero y cuenta qué entraría y qué está duplicado. No cambia nada.</summary>
    public async Task<Result<ImportPreview>> PreviewAsync(string json, CancellationToken ct = default)
    {
        var parsed = Parse(json);
        if (!parsed.IsSuccess)
        {
            return Result<ImportPreview>.Fail(parsed.Error!);
        }

        var incoming = parsed.Value!;
        var current = await context.GetAsync(ct);
        var map = DuplicateTechniqueMap(current, incoming);
        var sessionIds = current.Sessions.Active().Select(s => s.Id).ToHashSet();

        return Result<ImportPreview>.Ok(new ImportPreview(
            incoming.SchemaVersion,
            incoming.Profile is not null,
            incoming.Techniques.Active().Count(),
            incoming.Sessions.Active().Count(),
            incoming.Positions.Count(p => !p.IsDeleted),
            map.Count,
            incoming.Sessions.Active().Count(s => sessionIds.Contains(s.Id))));
    }

    /// <summary>
    /// Replace: sustituye todo. Merge: añade lo nuevo; las técnicas duplicadas (mismo nombre y posición) se
    /// reutilizan y las sesiones con el mismo Id se omiten.
    /// </summary>
    public async Task<Result> ImportAsync(string json, ImportMode mode, CancellationToken ct = default)
    {
        var parsed = Parse(json);
        if (!parsed.IsSuccess)
        {
            return parsed;
        }

        var incoming = parsed.Value!;
        incoming.ExportedAt = null;
        if (mode == ImportMode.Replace)
        {
            await context.ReplaceAsync(incoming, notify: true, ct);
            return Result.Ok();
        }

        var current = await context.GetAsync(ct);
        current.Profile ??= incoming.Profile;

        var positionMap = new Dictionary<Guid, Guid>();
        foreach (var p in incoming.Positions)
        {
            var same = current.Positions.FirstOrDefault(x => x.Id == p.Id
                || (!x.IsDeleted && TextNormalizer.Normalize(x.Name) == TextNormalizer.Normalize(p.Name)));
            if (same is null)
            {
                current.Positions.Add(p);
            }
            else
            {
                positionMap[p.Id] = same.Id;
            }
        }

        Guid? MapPosition(Guid? id) => id is { } v && positionMap.TryGetValue(v, out var m) ? m : id;

        var techniqueMap = DuplicateTechniqueMap(current, incoming);
        var existingTechniqueIds = current.Techniques.Select(t => t.Id).ToHashSet();
        foreach (var t in incoming.Techniques.Where(t => !techniqueMap.ContainsKey(t.Id) && !existingTechniqueIds.Contains(t.Id)))
        {
            t.PositionId = MapPosition(t.PositionId);
            current.Techniques.Add(t);
        }

        Guid MapTechnique(Guid id) => techniqueMap.TryGetValue(id, out var m) ? m : id;

        var existingSessionIds = current.Sessions.Select(s => s.Id).ToHashSet();
        foreach (var s in incoming.Sessions.Where(s => !existingSessionIds.Contains(s.Id)))
        {
            foreach (var st in s.Techniques)
            {
                st.TechniqueId = MapTechnique(st.TechniqueId);
            }

            foreach (var e in s.Rolls.SelectMany(r => r.Events))
            {
                e.TechniqueId = e.TechniqueId is { } tid ? MapTechnique(tid) : null;
                e.PositionId = MapPosition(e.PositionId);
            }

            current.Sessions.Add(s);
        }

        var existingLinkIds = current.Links.Select(l => l.Id).ToHashSet();
        current.Links.AddRange(incoming.Links.Where(l => !existingLinkIds.Contains(l.Id)));

        await context.SaveAsync(ct);
        return Result.Ok();
    }

    private static Result<AppData> Parse(string json)
    {
        AppData? data;
        try
        {
            data = AppDataSerializer.Deserialize(json);
        }
        catch (JsonException)
        {
            return Result<AppData>.Fail("El fichero no es un JSON válido de BJJ Journal.");
        }

        if (data is null)
        {
            return Result<AppData>.Fail("El fichero está vacío.");
        }

        if (data.SchemaVersion < 1 || data.SchemaVersion > AppData.CurrentSchemaVersion)
        {
            return Result<AppData>.Fail($"Versión de fichero no soportada ({data.SchemaVersion}).");
        }

        return Result<AppData>.Ok(data);
    }

    /// <summary>Técnicas del fichero que ya existen con otro Id (mismo nombre y posición): Id entrante → Id actual.</summary>
    private static Dictionary<Guid, Guid> DuplicateTechniqueMap(AppData current, AppData incoming)
    {
        var positionNames = current.Positions.Concat(incoming.Positions)
            .GroupBy(p => p.Id).ToDictionary(g => g.Key, g => TextNormalizer.Normalize(g.First().Name));
        string Key(Technique t) => TextNormalizer.Normalize(t.Name) + "|"
            + (t.PositionId is { } id && positionNames.TryGetValue(id, out var n) ? n : "");

        var existing = current.Techniques.Active()
            .GroupBy(Key).ToDictionary(g => g.Key, g => g.First().Id);
        var currentIds = current.Techniques.Select(t => t.Id).ToHashSet();

        return incoming.Techniques.Active()
            .Where(t => !currentIds.Contains(t.Id) && existing.ContainsKey(Key(t)))
            .ToDictionary(t => t.Id, t => existing[Key(t)]);
    }
}
