using BjjJournal.Domain.Enums;

namespace BjjJournal.Application.Dtos;

public sealed class SessionTechniqueInput
{
    public Guid TechniqueId { get; set; }
    public TechniqueFocus Focus { get; set; }
}
