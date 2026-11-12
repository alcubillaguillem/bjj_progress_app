using BjjJournal.Domain.Enums;

namespace BjjJournal.Application.Dtos;

public sealed record BeltProgress(Belt Belt, int Total, int CompetentOrMastered)
{
    public double Percent => Total == 0 ? 0 : 100.0 * CompetentOrMastered / Total;
}
