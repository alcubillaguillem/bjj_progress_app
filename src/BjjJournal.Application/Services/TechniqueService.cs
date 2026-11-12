using BjjJournal.Application.Dtos;
using BjjJournal.Application.Persistence;
using BjjJournal.Domain.Common;
using BjjJournal.Domain.Entities;
using BjjJournal.Domain.Enums;
using BjjJournal.Domain.Rules;

namespace BjjJournal.Application.Services;

/// <summary>F1 (biblioteca), F4 (búsqueda de técnicas) y F8 (checklist por cinturón).</summary>
public sealed class TechniqueService(JournalContext context, TimeProvider time)
{
    public async Task<IReadOnlyList<TechniqueListItem>> SearchAsync(TechniqueFilter filter, CancellationToken ct = default)
    {
        var data = await context.GetAsync(ct);
        var positions = data.Positions.ToDictionary(p => p.Id, p => p.Name);
        var usage = data.Sessions.TechniqueUsage();
        var query = TextNormalizer.Normalize(filter.Query);
        var tag = TextNormalizer.Normalize(filter.Tag);

        return data.Techniques.Active()
            .Where(t => filter.Category is null || t.Category == filter.Category)
            .Where(t => filter.PositionId is null || t.PositionId == filter.PositionId)
            .Where(t => filter.CurriculumBelt is null || t.CurriculumBelt == filter.CurriculumBelt)
            .Where(t => tag.Length == 0 || t.Tags.Any(x => TextNormalizer.Normalize(x) == tag))
            .Where(t => query.Length == 0 || Matches(t, PositionName(positions, t.PositionId), query))
            .Select(t =>
            {
                usage.TryGetValue(t.Id, out var u);
                return new TechniqueListItem(
                    t.Id, t.Name, t.Category, t.PositionId, PositionName(positions, t.PositionId), t.Tags,
                    t.Progress, t.CurriculumBelt, u.Count, u.Count == 0 ? null : u.Last);
            })
            .OrderBy(t => t.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    public async Task<TechniqueDetail?> GetAsync(Guid id, CancellationToken ct = default)
    {
        var data = await context.GetAsync(ct);
        var t = data.Techniques.FirstOrDefault(x => x.Id == id && !x.IsDeleted);
        if (t is null)
        {
            return null;
        }

        var positions = data.Positions.ToDictionary(p => p.Id, p => p.Name);
        var sessions = data.Sessions.Active().Where(s => s.TechniqueIds().Contains(id)).OrderByDescending(s => s.StartedAt).ToList();
        var events = data.Sessions.Active()
            .SelectMany(s => s.Rolls).Where(r => !r.IsDeleted)
            .SelectMany(r => r.Events).Where(e => !e.IsDeleted && e.TechniqueId == id)
            .ToList();

        return new TechniqueDetail(
            t.Id, t.Name, t.Category, t.PositionId, PositionName(positions, t.PositionId), t.CurriculumBelt, t.Progress,
            t.Description, t.Notes, t.Tags, t.Links,
            sessions.Count,
            sessions.Count == 0 ? null : sessions[0].StartedAt,
            events.Count(e => e.Kind == RollEventKind.SubmissionFor),
            events.Count(e => e.Kind == RollEventKind.SubmissionAgainst),
            sessions.Take(5).Select(s => s.ToListItem()).ToList());
    }

    public async Task<TechniqueInput?> GetInputAsync(Guid id, CancellationToken ct = default)
    {
        var data = await context.GetAsync(ct);
        var t = data.Techniques.FirstOrDefault(x => x.Id == id && !x.IsDeleted);
        return t is null
            ? null
            : new TechniqueInput
            {
                Name = t.Name,
                Category = t.Category,
                PositionId = t.PositionId,
                CurriculumBelt = t.CurriculumBelt,
                Progress = t.Progress,
                Description = t.Description,
                Notes = t.Notes,
                Tags = [.. t.Tags],
                Links = [.. t.Links],
            };
    }

    public async Task<Result<Guid>> CreateAsync(TechniqueInput input, CancellationToken ct = default)
    {
        var data = await context.GetAsync(ct);
        var now = time.GetUtcNow();
        var technique = new Technique { CreatedAt = now };
        var result = Apply(technique, input, data, now);
        if (!result.IsSuccess)
        {
            return Result<Guid>.Fail(result.Error!);
        }

        data.Techniques.Add(technique);
        await context.SaveAsync(ct);
        return Result<Guid>.Ok(technique.Id);
    }

    public async Task<Result> UpdateAsync(Guid id, TechniqueInput input, CancellationToken ct = default)
    {
        var data = await context.GetAsync(ct);
        var technique = data.Techniques.FirstOrDefault(x => x.Id == id && !x.IsDeleted);
        if (technique is null)
        {
            return Result.Fail("La técnica no existe.");
        }

        var result = Apply(technique, input, data, time.GetUtcNow());
        if (result.IsSuccess)
        {
            await context.SaveAsync(ct);
        }

        return result;
    }

    public async Task<Result> SetProgressAsync(Guid id, ProgressStatus progress, CancellationToken ct = default)
    {
        var data = await context.GetAsync(ct);
        var technique = data.Techniques.FirstOrDefault(x => x.Id == id && !x.IsDeleted);
        if (technique is null)
        {
            return Result.Fail("La técnica no existe.");
        }

        technique.Progress = progress;
        technique.UpdatedAt = time.GetUtcNow();
        await context.SaveAsync(ct);
        return Result.Ok();
    }

    /// <summary>Borrado lógico: sus apariciones en sesiones se conservan para el histórico.</summary>
    public async Task<Result> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var data = await context.GetAsync(ct);
        var technique = data.Techniques.FirstOrDefault(x => x.Id == id && !x.IsDeleted);
        if (technique is null)
        {
            return Result.Fail("La técnica no existe.");
        }

        technique.IsDeleted = true;
        technique.UpdatedAt = time.GetUtcNow();
        await context.SaveAsync(ct);
        return Result.Ok();
    }

    public async Task<IReadOnlyList<string>> ListTagsAsync(CancellationToken ct = default)
    {
        var data = await context.GetAsync(ct);
        return data.Techniques.Active()
            .SelectMany(t => t.Tags)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    /// <summary>Porcentaje de técnicas competentes o dominadas por cinturón del temario.</summary>
    public async Task<IReadOnlyList<BeltProgress>> GetBeltProgressAsync(CancellationToken ct = default)
    {
        var data = await context.GetAsync(ct);
        return data.Techniques.Active()
            .Where(t => t.CurriculumBelt is not null)
            .GroupBy(t => t.CurriculumBelt!.Value)
            .OrderBy(g => g.Key)
            .Select(g => new BeltProgress(
                g.Key,
                g.Count(),
                g.Count(t => t.Progress is ProgressStatus.Competent or ProgressStatus.Mastered)))
            .ToList();
    }

    private static Result Apply(Technique technique, TechniqueInput input, AppData data, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(input.Name))
        {
            return Result.Fail("El nombre de la técnica es obligatorio.");
        }

        var name = input.Name.Trim();
        var key = TextNormalizer.Normalize(name);
        var duplicated = data.Techniques.Active().Any(t =>
            t.Id != technique.Id && t.PositionId == input.PositionId && TextNormalizer.Normalize(t.Name) == key);
        if (duplicated)
        {
            return Result.Fail("Ya tienes una técnica con ese nombre en esa posición.");
        }

        technique.Name = name;
        technique.Category = input.Category;
        technique.PositionId = input.PositionId;
        technique.CurriculumBelt = input.CurriculumBelt;
        technique.Progress = input.Progress;
        technique.Description = Projections.CleanText(input.Description);
        technique.Notes = Projections.CleanText(input.Notes);
        technique.Tags = Projections.CleanList(input.Tags);
        technique.Links = Projections.CleanList(input.Links);
        technique.UpdatedAt = now;
        return DomainRules.Validate(technique);
    }

    private static bool Matches(Technique t, string? positionName, string query) =>
        TextNormalizer.Contains(t.Name, query)
        || TextNormalizer.Contains(positionName, query)
        || TextNormalizer.Contains(t.Notes, query)
        || TextNormalizer.Contains(t.Description, query)
        || t.Tags.Any(tag => TextNormalizer.Contains(tag, query));

    private static string? PositionName(Dictionary<Guid, string> positions, Guid? id) =>
        id is { } value && positions.TryGetValue(value, out var name) ? name : null;
}
