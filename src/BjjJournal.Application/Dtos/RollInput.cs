using BjjJournal.Domain.Enums;

namespace BjjJournal.Application.Dtos;

public sealed class RollInput
{
    public string? PartnerName { get; set; }
    public Belt? PartnerBelt { get; set; }
    public int DurationMinutes { get; set; } = 6;
    public List<RollEventInput> Events { get; set; } = [];
}
