using BjjJournal.Domain.Common;
using BjjJournal.Domain.Enums;

namespace BjjJournal.Domain.Entities;

/// <summary>Cada combate o rodada de la sesión.</summary>
public class Roll : Entity
{
    public Guid SessionId { get; set; }
    public string? PartnerName { get; set; }
    public Belt? PartnerBelt { get; set; }
    public int DurationMinutes { get; set; }
    public List<RollEvent> Events { get; set; } = [];
}
