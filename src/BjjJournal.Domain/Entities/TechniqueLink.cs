using BjjJournal.Domain.Common;
using BjjJournal.Domain.Enums;

namespace BjjJournal.Domain.Entities;

/// <summary>Arista del mapa de técnicas (fase 2).</summary>
public class TechniqueLink : Entity
{
    public Guid FromTechniqueId { get; set; }
    public Guid ToTechniqueId { get; set; }
    public LinkKind Kind { get; set; }
}
