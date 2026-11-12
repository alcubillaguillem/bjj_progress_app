using BjjJournal.Domain.Enums;

namespace BjjJournal.Application.Dtos;

public sealed class ProfileInput
{
    public string DisplayName { get; set; } = "";
    public Belt Belt { get; set; }
    public int Stripes { get; set; }
    public string? GymName { get; set; }
    public DateOnly? StartedTraining { get; set; }
}
