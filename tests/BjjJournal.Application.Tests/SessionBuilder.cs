using BjjJournal.Application.Dtos;
using BjjJournal.Domain.Enums;

namespace BjjJournal.Application.Tests;

internal static class SessionBuilder
{
    public static SessionInput At(
        DateTimeOffset when,
        int minutes = 60,
        int satisfaction = 3,
        SessionType type = SessionType.Class,
        Attire attire = Attire.Gi,
        Guid? tech = null) => new()
    {
        StartedAt = when,
        DurationMinutes = minutes,
        Satisfaction = satisfaction,
        Type = type,
        Attire = attire,
        Techniques = tech is { } id ? [new SessionTechniqueInput { TechniqueId = id, Focus = TechniqueFocus.Drilled }] : [],
    };
}
