using BjjJournal.Domain.Enums;

namespace BjjJournal.Application.Dtos;

public sealed record ImportPreview(
    int SchemaVersion,
    bool HasProfile,
    int Techniques,
    int Sessions,
    int Positions,
    int DuplicateTechniques,
    int DuplicateSessions);
