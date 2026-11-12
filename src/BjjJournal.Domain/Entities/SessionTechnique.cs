using BjjJournal.Domain.Enums;

namespace BjjJournal.Domain.Entities;

/// <summary>Técnica trabajada en una sesión y con qué foco.</summary>
public class SessionTechnique
{
    public Guid SessionId { get; set; }
    public Guid TechniqueId { get; set; }
    public TechniqueFocus Focus { get; set; }
}
