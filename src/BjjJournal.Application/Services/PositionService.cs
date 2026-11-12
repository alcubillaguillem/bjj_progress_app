using BjjJournal.Application.Dtos;
using BjjJournal.Application.Persistence;
using BjjJournal.Domain.Common;
using BjjJournal.Domain.Entities;
using BjjJournal.Domain.Enums;
using BjjJournal.Domain.Rules;

namespace BjjJournal.Application.Services;

public sealed class PositionService(JournalContext context, TimeProvider time)
{
    public async Task<IReadOnlyList<PositionDto>> ListAsync(CancellationToken ct = default)
    {
        var data = await context.GetAsync(ct);
        return data.Positions
            .Where(p => !p.IsDeleted)
            .OrderBy(p => p.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(p => new PositionDto(p.Id, p.Name, p.Role))
            .ToList();
    }

    /// <summary>Crea una posición. Si ya existe una con el mismo nombre (sin tildes ni mayúsculas), devuelve esa.</summary>
    public async Task<Result<PositionDto>> CreateAsync(string name, PositionRole role, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Result<PositionDto>.Fail("El nombre de la posición es obligatorio.");
        }

        var data = await context.GetAsync(ct);
        var key = TextNormalizer.Normalize(name);
        var existing = data.Positions.FirstOrDefault(p => !p.IsDeleted && TextNormalizer.Normalize(p.Name) == key);
        if (existing is not null)
        {
            return Result<PositionDto>.Ok(new PositionDto(existing.Id, existing.Name, existing.Role));
        }

        var now = time.GetUtcNow();
        var position = new Position { Name = name.Trim(), Role = role, CreatedAt = now, UpdatedAt = now };
        data.Positions.Add(position);
        await context.SaveAsync(ct);
        return Result<PositionDto>.Ok(new PositionDto(position.Id, position.Name, position.Role));
    }
}
