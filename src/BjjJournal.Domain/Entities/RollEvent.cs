using BjjJournal.Domain.Common;
using BjjJournal.Domain.Enums;

namespace BjjJournal.Domain.Entities;

public class RollEvent : Entity
{
    public Guid RollId { get; set; }
    public RollEventKind Kind { get; set; }
    public Guid? TechniqueId { get; set; }
    public Guid? PositionId { get; set; }
}
