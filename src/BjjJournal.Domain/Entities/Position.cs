using BjjJournal.Domain.Common;
using BjjJournal.Domain.Enums;

namespace BjjJournal.Domain.Entities;

/// <summary>Montada, guardia cerrada, espalda...</summary>
public class Position : Entity
{
    public string Name { get; set; } = "";
    public PositionRole Role { get; set; }
}
