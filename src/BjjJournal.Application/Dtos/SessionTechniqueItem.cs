using BjjJournal.Domain.Enums;

namespace BjjJournal.Application.Dtos;

public sealed record SessionTechniqueItem(Guid TechniqueId, string Name, TechniqueCategory Category, TechniqueFocus Focus);
