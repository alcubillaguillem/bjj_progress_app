using BjjJournal.Domain.Common;
using BjjJournal.Domain.Entities;

namespace BjjJournal.Domain.Rules;

/// <summary>Reglas de la sección 5 del documento de contexto.</summary>
public static class DomainRules
{
    public const int MinSatisfaction = 1;
    public const int MaxSatisfaction = 5;
    public const int MinStripes = 0;
    public const int MaxStripes = 4;

    public static bool IsValidSatisfaction(int value) => value is >= MinSatisfaction and <= MaxSatisfaction;

    public static bool IsValidStripes(int value) => value is >= MinStripes and <= MaxStripes;

    public static bool IsValidDuration(int minutes) => minutes > 0;

    public static Result Validate(UserProfile profile)
    {
        if (string.IsNullOrWhiteSpace(profile.DisplayName))
        {
            return Result.Fail("El nombre es obligatorio.");
        }

        return IsValidStripes(profile.Stripes) ? Result.Ok() : Result.Fail("Los grados van de 0 a 4.");
    }

    public static Result Validate(TrainingSession session)
    {
        if (!IsValidDuration(session.DurationMinutes))
        {
            return Result.Fail("La duración debe ser mayor que 0 minutos.");
        }

        if (!IsValidSatisfaction(session.Satisfaction))
        {
            return Result.Fail("La satisfacción va de 1 a 5.");
        }

        foreach (var roll in session.Rolls)
        {
            if (!IsValidDuration(roll.DurationMinutes))
            {
                return Result.Fail("Cada roll debe durar más de 0 minutos.");
            }
        }

        return Result.Ok();
    }

    public static Result Validate(Technique technique)
    {
        return string.IsNullOrWhiteSpace(technique.Name)
            ? Result.Fail("El nombre de la técnica es obligatorio.")
            : Result.Ok();
    }

    /// <summary>Un enlace no puede unir una técnica consigo misma ni duplicar el mismo par y tipo.</summary>
    public static Result CanAddLink(TechniqueLink link, IEnumerable<TechniqueLink> existing)
    {
        if (link.FromTechniqueId == link.ToTechniqueId)
        {
            return Result.Fail("Una técnica no puede enlazarse consigo misma.");
        }

        var duplicated = existing.Any(l => !l.IsDeleted
            && l.FromTechniqueId == link.FromTechniqueId
            && l.ToTechniqueId == link.ToTechniqueId
            && l.Kind == link.Kind);

        return duplicated ? Result.Fail("Ese enlace ya existe.") : Result.Ok();
    }
}
