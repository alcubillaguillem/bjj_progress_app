using BjjJournal.Domain.Enums;

namespace BjjJournal.Application.Dtos;

public sealed class RollEventInput
{
    public RollEventKind Kind { get; set; }
    public Guid? TechniqueId { get; set; }
    public Guid? PositionId { get; set; }
}
