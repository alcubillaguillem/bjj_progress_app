using BjjJournal.Domain.Enums;

namespace BjjJournal.App.Ui;

/// <summary>Icono de cada categoría de técnica (el color está en <see cref="Labels.CategoryColor"/>).</summary>
public static class CategoryStyle
{
    public static string Icon(TechniqueCategory c) => c switch
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
}
