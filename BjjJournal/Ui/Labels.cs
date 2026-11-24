using BjjJournal.Models;

namespace BjjJournal.Ui;

/// <summary>Textos de UI para los enums del dominio (en español; <see cref="Loc"/> los traduce).</summary>
public static class Labels
{
    private static string T(string es) => Loc.Current[es];

    public static string Of(Belt v) => v switch
    {
        Belt.White => T("Blanco"),
        Belt.Blue => T("Azul"),
        Belt.Purple => T("Morado"),
        Belt.Brown => T("Marrón"),
        Belt.Black => T("Negro"),
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
        TechniqueCategory.Submission => T("Sumisión"),
        TechniqueCategory.Sweep => T("Barrida"),
        TechniqueCategory.Pass => T("Pase"),
        TechniqueCategory.Escape => T("Escape"),
        TechniqueCategory.Takedown => T("Derribo"),
        TechniqueCategory.Transition => T("Transición"),
        TechniqueCategory.Guard => T("Guardia"),
        TechniqueCategory.Control => T("Control"),
        TechniqueCategory.Defense => T("Defensa"),
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

    public static string Of(Attire v) => v == Attire.Gi ? "Gi" : "No-gi";

    public static string Of(SessionType v) => v switch
    {
        SessionType.Class => T("Clase"),
        SessionType.OpenMat => T("Open mat"),
        SessionType.Drilling => T("Drilling"),
        SessionType.Competition => T("Competición"),
        SessionType.Private => T("Particular"),
        _ => v.ToString(),
    };

    public static string Of(TechniqueFocus v) => v switch
    {
        TechniqueFocus.Learned => T("Aprendida"),
        TechniqueFocus.Drilled => T("Repetida"),
        TechniqueFocus.UsedInRoll => T("Usada en roll"),
        _ => v.ToString(),
    };

    public static string Of(RollEventKind v) => v switch
    {
        RollEventKind.SubmissionFor => T("Sumisión a favor"),
        RollEventKind.SubmissionAgainst => T("Sumisión en contra"),
        RollEventKind.SweepFor => T("Barrida"),
        RollEventKind.PassFor => T("Pase"),
        RollEventKind.Escape => T("Escape"),
        _ => v.ToString(),
    };

    public static string Of(ProgressStatus v) => v switch
    {
        ProgressStatus.NotStarted => T("Sin empezar"),
        ProgressStatus.Learning => T("Aprendiendo"),
        ProgressStatus.Competent => T("Competente"),
        ProgressStatus.Mastered => T("Dominada"),
        _ => v.ToString(),
    };

    public static string Satisfaction(int v) => v switch
    {
        1 => T("Muy mala"),
        2 => T("Mala"),
        3 => T("Normal"),
        4 => T("Buena"),
        5 => T("Muy buena"),
        _ => "",
    };

    /// <summary>Icono de cada categoría.</summary>
    public static string CategoryIcon(TechniqueCategory c) => c switch
    {
        TechniqueCategory.Submission => "target",
        TechniqueCategory.Sweep => "sweep",
        TechniqueCategory.Pass => "pass",
        TechniqueCategory.Escape => "escape",
        TechniqueCategory.Takedown => "takedown",
        TechniqueCategory.Transition => "transition",
        TechniqueCategory.Guard => "guard",
        TechniqueCategory.Control => "lock",
        TechniqueCategory.Defense => "shield",
        _ => "book",
    };

    /// <summary>Slug para la URL (/tecnicas/categoria/sumision…).</summary>
    public static string Slug(TechniqueCategory c) => c.ToString().ToLowerInvariant();

    public static TechniqueCategory? FromSlug(string? slug) =>
        Enum.GetValues<TechniqueCategory>().Cast<TechniqueCategory?>().FirstOrDefault(c => Slug(c!.Value) == slug?.ToLowerInvariant());

    /// <summary>Color por tipo de sesión (calendario y detalle).</summary>
    public static string TypeColor(SessionType t) => t switch
    {
        SessionType.Class => "#c5f04a",
        SessionType.OpenMat => "#38bdf8",
        SessionType.Drilling => "#facc15",
        SessionType.Competition => "#f87171",
        SessionType.Private => "#a78bfa",
        _ => "#9a9aa3",
    };
}
