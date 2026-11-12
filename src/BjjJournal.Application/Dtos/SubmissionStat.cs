using BjjJournal.Domain.Enums;

namespace BjjJournal.Application.Dtos;

public sealed record SubmissionStat(Guid? TechniqueId, string Name, int For, int Against);
