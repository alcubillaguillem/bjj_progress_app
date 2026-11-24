using System.Text.Json.Serialization;

namespace BjjJournal.Models;

// Todo el modelo de datos de la app. Se guarda tal cual como JSON (localStorage, nube y copias de seguridad).

/// <summary>Base de lo que se sincroniza: Id propio, fecha del último cambio y borrado lógico.</summary>
public abstract class Entity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTimeOffset UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }
}

/// <summary>El diario completo: un solo documento.</summary>
public class AppData
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;
    public UserProfile? Profile { get; set; }
    public List<Position> Positions { get; set; } = [];
    public List<Technique> Techniques { get; set; } = [];
    public List<TrainingSession> Sessions { get; set; } = [];
}

public class UserProfile : Entity
{
    public string DisplayName { get; set; } = "";
    public Belt Belt { get; set; }
    public int Stripes { get; set; }
    public string? GymName { get; set; }
    public DateOnly? StartedTraining { get; set; }
}

/// <summary>Montada, guardia cerrada, espalda...</summary>
public class Position : Entity
{
    public string Name { get; set; } = "";
}

public class Technique : Entity
{
    public string Name { get; set; } = "";
    public TechniqueCategory Category { get; set; }
    public Guid? PositionId { get; set; }
    public Belt? CurriculumBelt { get; set; }
    public ProgressStatus Progress { get; set; }
    public string? Description { get; set; }
    public string? Notes { get; set; }
    public List<string> Tags { get; set; } = [];
    public List<string> Links { get; set; } = [];
}

public class TrainingSession : Entity
{
    public DateTimeOffset StartedAt { get; set; }
    public int DurationMinutes { get; set; } = 90;
    public SessionType Type { get; set; }
    public Attire Attire { get; set; }
    public string? Location { get; set; }
    public int Satisfaction { get; set; } = 3;
    public string? Notes { get; set; }
    public List<SessionTechnique> Techniques { get; set; } = [];
    public List<Roll> Rolls { get; set; } = [];

    /// <summary>Día de la sesión en la hora local del dispositivo.</summary>
    [JsonIgnore] public DateOnly Day => DateOnly.FromDateTime(StartedAt.LocalDateTime);

    [JsonIgnore] public IEnumerable<RollEvent> Events => Rolls.SelectMany(r => r.Events);

    public int Count(RollEventKind kind) => Events.Count(e => e.Kind == kind);

    /// <summary>Técnicas de la sesión: las de la lista y las que salen en los rolls.</summary>
    public HashSet<Guid> TechniqueIds() =>
        [.. Techniques.Select(t => t.TechniqueId), .. Events.Where(e => e.TechniqueId != null).Select(e => e.TechniqueId!.Value)];
}

/// <summary>Técnica trabajada en una sesión y con qué foco.</summary>
public class SessionTechnique
{
    public Guid TechniqueId { get; set; }
    public TechniqueFocus Focus { get; set; } = TechniqueFocus.Drilled;
}

/// <summary>Cada combate de la sesión.</summary>
public class Roll
{
    public string? PartnerName { get; set; }
    public Belt? PartnerBelt { get; set; }
    public int DurationMinutes { get; set; } = 6;
    public List<RollEvent> Events { get; set; } = [];
}

/// <summary>Algo que pasó en un roll: sumisión, barrida, pase...</summary>
public class RollEvent
{
    public RollEventKind Kind { get; set; }
    public Guid? TechniqueId { get; set; }
    public Guid? PositionId { get; set; }
}

public enum Belt { White, Blue, Purple, Brown, Black }

public enum Attire { Gi, NoGi }

public enum SessionType { Class, OpenMat, Drilling, Competition, Private }

public enum TechniqueCategory { Submission, Sweep, Pass, Escape, Takedown, Transition, Guard, Control, Defense }

public enum TechniqueFocus { Learned, Drilled, UsedInRoll }

public enum ProgressStatus { NotStarted, Learning, Competent, Mastered }

public enum RollEventKind { SubmissionFor, SubmissionAgainst, SweepFor, PassFor, Escape }
