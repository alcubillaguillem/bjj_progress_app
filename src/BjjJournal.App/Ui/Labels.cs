using BjjJournal.Domain.Enums;

namespace BjjJournal.App.Ui;

/// <summary>Textos de UI en español para los enums del dominio.</summary>
public static class Labels
{
    public static string Of(Belt v) => v switch
    {
        Belt.White => "Blanco",
        Belt.Blue => "Azul",
        Belt.Purple => "Morado",
        Belt.Brown => "Marrón",
        Belt.Black => "Negro",
        _ => v.ToString(),
    };

    public static string BeltColor(Belt v) => v switch
    {
        Belt.White => "#e9e9ee",
        Belt.Blue => "#3b82f6",
        Belt.Purple => "#a855f7",
        Belt.Brown => "#a16207",
        Belt.Black => "#27272a",
        _ => "#888",
    };

    public static string Of(TechniqueCategory v) => v switch
    {
        TechniqueCategory.Submission => "Sumisión",
        TechniqueCategory.Sweep => "Barrida",
        TechniqueCategory.Pass => "Pase",
        TechniqueCategory.Escape => "Escape",
        TechniqueCategory.Takedown => "Derribo",
        TechniqueCategory.Transition => "Transición",
        TechniqueCategory.Guard => "Guardia",
        TechniqueCategory.Control => "Control",
        TechniqueCategory.Defense => "Defensa",
        _ => v.ToString(),
    };

    /// <summary>Color por categoría (mismo orden en toda la app).</summary>
    public static string CategoryColor(TechniqueCategory v) => v switch
    {
        TechniqueCategory.Submission => "#c5f04a",
        TechniqueCategory.Sweep => "#38bdf8",
        TechniqueCategory.Pass => "#f472b6",
        TechniqueCategory.Escape => "#fdba74",
        TechniqueCategory.Takedown => "#a78bfa",
        TechniqueCategory.Transition => "#2dd4bf",
        TechniqueCategory.Guard => "#facc15",
        TechniqueCategory.Control => "#94a3b8",
        TechniqueCategory.Defense => "#f87171",
        _ => "#888",
    };

    public static string Of(PositionRole v) => v switch
    {
        PositionRole.Top => "Arriba",
        PositionRole.Bottom => "Abajo",
        PositionRole.Neutral => "Neutral",
        _ => v.ToString(),
    };

    public static string Of(Attire v) => v == Attire.Gi ? "Gi" : "No-gi";

    public static string Of(SessionType v) => v switch
    {
        SessionType.Class => "Clase",
        SessionType.OpenMat => "Open mat",
        SessionType.Drilling => "Drilling",
        SessionType.Competition => "Competición",
        SessionType.Private => "Particular",
        _ => v.ToString(),
    };

    public static string Of(TechniqueFocus v) => v switch
    {
        TechniqueFocus.Learned => "Aprendida",
        TechniqueFocus.Drilled => "Repetida",
        TechniqueFocus.UsedInRoll => "Usada en roll",
        _ => v.ToString(),
    };

    public static string Of(RollEventKind v) => v switch
    {
        RollEventKind.SubmissionFor => "Sumisión a favor",
        RollEventKind.SubmissionAgainst => "Sumisión en contra",
        RollEventKind.SweepFor => "Barrida",
        RollEventKind.PassFor => "Pase",
        RollEventKind.Escape => "Escape",
        _ => v.ToString(),
    };

    public static string Of(ProgressStatus v) => v switch
    {
        ProgressStatus.NotStarted => "Sin empezar",
        ProgressStatus.Learning => "Aprendiendo",
        ProgressStatus.Competent => "Competente",
        ProgressStatus.Mastered => "Dominada",
        _ => v.ToString(),
    };

    public static string Satisfaction(int v) => v switch
    {
        1 => "Muy mala",
        2 => "Mala",
        3 => "Normal",
        4 => "Buena",
        5 => "Muy buena",
        _ => "",
    };
}
