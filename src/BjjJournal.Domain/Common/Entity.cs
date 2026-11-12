namespace BjjJournal.Domain.Common;

/// <summary>Base de todas las entidades: Id generado en el cliente, auditoría y borrado lógico (para sincronizar en la fase 3).</summary>
public abstract class Entity
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }
}
