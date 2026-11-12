using BjjJournal.Domain.Common;
using BjjJournal.Domain.Enums;

namespace BjjJournal.Domain.Entities;

public class TrainingSession : Entity
{
    public DateTimeOffset StartedAt { get; set; }
    public int DurationMinutes { get; set; }
    public SessionType Type { get; set; }
    public Attire Attire { get; set; }
    public string? Location { get; set; }
    public int Satisfaction { get; set; }
    public string? Notes { get; set; }
    public List<SessionTechnique> Techniques { get; set; } = [];
    public List<Roll> Rolls { get; set; } = [];
}
