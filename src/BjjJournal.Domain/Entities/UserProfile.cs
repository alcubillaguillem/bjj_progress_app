using BjjJournal.Domain.Common;
using BjjJournal.Domain.Enums;

namespace BjjJournal.Domain.Entities;

public class UserProfile : Entity
{
    public string DisplayName { get; set; } = "";
    public Belt Belt { get; set; }
    public int Stripes { get; set; }
    public string? GymName { get; set; }
    public DateOnly? StartedTraining { get; set; }
}
