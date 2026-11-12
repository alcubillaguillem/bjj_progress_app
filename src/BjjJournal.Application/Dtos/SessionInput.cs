using BjjJournal.Domain.Enums;

namespace BjjJournal.Application.Dtos;

public sealed class SessionInput
{
    public DateTimeOffset StartedAt { get; set; }
    public int DurationMinutes { get; set; } = 90;
    public SessionType Type { get; set; }
    public Attire Attire { get; set; }
    public string? Location { get; set; }
    public int Satisfaction { get; set; } = 3;
    public string? Notes { get; set; }
    public List<SessionTechniqueInput> Techniques { get; set; } = [];
    public List<RollInput> Rolls { get; set; } = [];
}
