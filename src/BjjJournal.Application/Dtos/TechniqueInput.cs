using BjjJournal.Domain.Enums;

namespace BjjJournal.Application.Dtos;

public sealed class TechniqueInput
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
