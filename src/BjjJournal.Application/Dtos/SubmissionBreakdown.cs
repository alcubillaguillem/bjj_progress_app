namespace BjjJournal.Application.Dtos;

/// <summary>Sumisiones de un periodo: total, técnicas distintas y ranking con porcentaje.</summary>
public sealed record SubmissionBreakdown(int Total, int Unique, IReadOnlyList<SubmissionSlice> Items);

/// <summary>Una fila del ranking. Position solo se rellena cuando se agrupa por posición.</summary>
public sealed record SubmissionSlice(Guid? TechniqueId, string Name, string? Position, int Count, double Share);
