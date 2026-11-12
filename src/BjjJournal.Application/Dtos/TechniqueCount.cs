using BjjJournal.Domain.Enums;

namespace BjjJournal.Application.Dtos;

public sealed record TechniqueCount(Guid TechniqueId, string Name, TechniqueCategory Category, int Count);
