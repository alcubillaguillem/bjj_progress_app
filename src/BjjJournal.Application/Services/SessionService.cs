using BjjJournal.Application.Dtos;
using BjjJournal.Application.Persistence;
using BjjJournal.Domain.Common;
using BjjJournal.Domain.Entities;
using BjjJournal.Domain.Enums;
using BjjJournal.Domain.Rules;

namespace BjjJournal.Application.Services;

/// <summary>F2 (registro de sesiones), F3 (rolls y eventos) y F4 (filtros de sesiones).</summary>
public sealed class SessionService(JournalContext context, TimeProvider time)
{
    public async Task<IReadOnlyList<SessionListItem>> ListAsync(SessionFilter filter, CancellationToken ct = default)
    {
        var data = await context.GetAsync(ct);
        return data.Sessions.Active()
            .Where(s => filter.Type is null || s.Type == filter.Type)
            .Where(s => filter.Attire is null || s.Attire == filter.Attire)
            .Where(s => filter.MinSatisfaction is null || s.Satisfaction >= filter.MinSatisfaction)
            .Where(s => filter.From is null || time.LocalDate(s.StartedAt) >= filter.From)
            .Where(s => filter.To is null || time.LocalDate(s.StartedAt) <= filter.To)
            .OrderByDescending(s => s.StartedAt)
            .Select(s => s.ToListItem())
            .ToList();
    }

    public async Task<SessionDetail?> GetAsync(Guid id, CancellationToken ct = default)
    {
        var data = await context.GetAsync(ct);
        var s = data.Sessions.FirstOrDefault(x => x.Id == id && !x.IsDeleted);
        if (s is null)
        {
            return null;
        }

        // Incluye técnicas borradas: el histórico se conserva.
        var techniques = data.Techniques.ToDictionary(t => t.Id);
        var positions = data.Positions.ToDictionary(p => p.Id, p => p.Name);

        var sessionTechniques = s.Techniques
            .Where(st => techniques.ContainsKey(st.TechniqueId))
            .Select(st => new SessionTechniqueItem(
                st.TechniqueId, techniques[st.TechniqueId].Name, techniques[st.TechniqueId].Category, st.Focus))
            .ToList();

        var rolls = s.Rolls.Where(r => !r.IsDeleted)
            .Select(r => new RollItem(
                r.Id,
                r.PartnerName,
                r.PartnerBelt,
                r.DurationMinutes,
                r.Events.Where(e => !e.IsDeleted)
                    .Select(e => new RollEventItem(
                        e.Kind,
                        e.TechniqueId,
                        e.TechniqueId is { } tid && techniques.TryGetValue(tid, out var t) ? t.Name : null,
                        e.PositionId,
                        e.PositionId is { } pid && positions.TryGetValue(pid, out var p) ? p : null))
                    .ToList()))
            .ToList();

        return new SessionDetail(
            s.Id, s.StartedAt, s.DurationMinutes, s.Type, s.Attire, s.Location, s.Satisfaction, s.Notes,
            sessionTechniques, rolls,
            s.CountEvents(RollEventKind.SubmissionFor),
            s.CountEvents(RollEventKind.SubmissionAgainst));
    }

    public async Task<SessionInput?> GetInputAsync(Guid id, CancellationToken ct = default)
    {
        var data = await context.GetAsync(ct);
        var s = data.Sessions.FirstOrDefault(x => x.Id == id && !x.IsDeleted);
        if (s is null)
        {
            return null;
        }

        return new SessionInput
        {
            StartedAt = s.StartedAt,
            DurationMinutes = s.DurationMinutes,
            Type = s.Type,
            Attire = s.Attire,
            Location = s.Location,
            Satisfaction = s.Satisfaction,
            Notes = s.Notes,
            Techniques = s.Techniques.Select(t => new SessionTechniqueInput { TechniqueId = t.TechniqueId, Focus = t.Focus }).ToList(),
            Rolls = s.Rolls.Where(r => !r.IsDeleted).Select(r => new RollInput
            {
                PartnerName = r.PartnerName,
                PartnerBelt = r.PartnerBelt,
                DurationMinutes = r.DurationMinutes,
                Events = r.Events.Where(e => !e.IsDeleted)
                    .Select(e => new RollEventInput { Kind = e.Kind, TechniqueId = e.TechniqueId, PositionId = e.PositionId })
                    .ToList(),
            }).ToList(),
        };
    }

    /// <summary>Valores por defecto del formulario: ahora, la última duración, el último tipo y gi/no-gi usados.</summary>
    public async Task<SessionInput> GetDefaultsAsync(CancellationToken ct = default)
    {
        var data = await context.GetAsync(ct);
        var last = data.Sessions.Active().MaxBy(s => s.CreatedAt);
        var now = TimeZoneInfo.ConvertTime(time.GetUtcNow(), time.LocalTimeZone);
        return new SessionInput
        {
            StartedAt = new DateTimeOffset(now.Year, now.Month, now.Day, now.Hour, now.Minute, 0, now.Offset),
            DurationMinutes = last?.DurationMinutes ?? 90,
            Type = last?.Type ?? SessionType.Class,
            Attire = last?.Attire ?? Attire.Gi,
            Location = last?.Location,
            Satisfaction = 3,
        };
    }

    public async Task<Result<Guid>> CreateAsync(SessionInput input, CancellationToken ct = default)
    {
        var data = await context.GetAsync(ct);
        var now = time.GetUtcNow();
        var session = Build(input, new TrainingSession { CreatedAt = now }, now);
        var validation = DomainRules.Validate(session);
        if (!validation.IsSuccess)
        {
            return Result<Guid>.Fail(validation.Error!);
        }

        data.Sessions.Add(session);
        await context.SaveAsync(ct);
        return Result<Guid>.Ok(session.Id);
    }

    public async Task<Result> UpdateAsync(Guid id, SessionInput input, CancellationToken ct = default)
    {
        var data = await context.GetAsync(ct);
        var index = data.Sessions.FindIndex(x => x.Id == id && !x.IsDeleted);
        if (index < 0)
        {
            return Result.Fail("La sesión no existe.");
        }

        var existing = data.Sessions[index];
        var updated = Build(input, new TrainingSession { Id = existing.Id, CreatedAt = existing.CreatedAt }, time.GetUtcNow());
        var validation = DomainRules.Validate(updated);
        if (!validation.IsSuccess)
        {
            return validation;
        }

        data.Sessions[index] = updated;
        await context.SaveAsync(ct);
        return Result.Ok();
    }

    public async Task<Result> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var data = await context.GetAsync(ct);
        var session = data.Sessions.FirstOrDefault(x => x.Id == id && !x.IsDeleted);
        if (session is null)
        {
            return Result.Fail("La sesión no existe.");
        }

        session.IsDeleted = true;
        session.UpdatedAt = time.GetUtcNow();
        await context.SaveAsync(ct);
        return Result.Ok();
    }

    private static TrainingSession Build(SessionInput input, TrainingSession session, DateTimeOffset now)
    {
        session.StartedAt = input.StartedAt.ToUniversalTime();
        session.DurationMinutes = input.DurationMinutes;
        session.Type = input.Type;
        session.Attire = input.Attire;
        session.Location = Projections.CleanText(input.Location);
        session.Satisfaction = input.Satisfaction;
        session.Notes = Projections.CleanText(input.Notes);
        session.UpdatedAt = now;

        // Una técnica solo una vez por sesión.
        session.Techniques = input.Techniques
            .Where(t => t.TechniqueId != Guid.Empty)
            .GroupBy(t => t.TechniqueId)
            .Select(g => new SessionTechnique { SessionId = session.Id, TechniqueId = g.Key, Focus = g.First().Focus })
            .ToList();

        session.Rolls = input.Rolls.Select(r =>
        {
            var roll = new Roll
            {
                SessionId = session.Id,
                PartnerName = Projections.CleanText(r.PartnerName),
                PartnerBelt = r.PartnerBelt,
                DurationMinutes = r.DurationMinutes,
                CreatedAt = now,
                UpdatedAt = now,
            };
            roll.Events = r.Events.Select(e => new RollEvent
            {
                RollId = roll.Id,
                Kind = e.Kind,
                TechniqueId = e.TechniqueId,
                PositionId = e.PositionId,
                CreatedAt = now,
                UpdatedAt = now,
            }).ToList();
            return roll;
        }).ToList();

        return session;
    }
}
