using BjjJournal.Domain.Entities;

namespace BjjJournal.Application.Persistence;

/// <summary>Documento completo del diario. Es lo que se guarda en el dispositivo y lo que se exporta.</summary>
public sealed class AppData
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;
    public DateTimeOffset? ExportedAt { get; set; }
    public UserProfile? Profile { get; set; }
    public List<Position> Positions { get; set; } = [];
    public List<Technique> Techniques { get; set; } = [];
    public List<TechniqueLink> Links { get; set; } = [];
    public List<TrainingSession> Sessions { get; set; } = [];
}
