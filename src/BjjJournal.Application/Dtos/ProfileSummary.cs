namespace BjjJournal.Application.Dtos;

/// <summary>Datos de la pantalla de perfil: contadores, nivel y los dos radares.</summary>
public sealed record ProfileSummary(
    int Sessions,
    double Hours,
    int Rolls,
    int SubmissionsFor,
    int SubmissionsAgainst,
    int StreakWeeks,
    LevelInfo Level,
    IReadOnlyList<SubmissionSlice> TopSubmissions,
    SessionMix Mix);

/// <summary>Nivel por experiencia. Xp acumulada; LevelStartXp y NextLevelXp delimitan el nivel actual.</summary>
public sealed record LevelInfo(int Level, int Xp, int LevelStartXp, int NextLevelXp)
{
    public double Progress => NextLevelXp == LevelStartXp ? 0 : (double)(Xp - LevelStartXp) / (NextLevelXp - LevelStartXp);
}

/// <summary>Reparto de sesiones para el radar: equipación y tipo.</summary>
public sealed record SessionMix(int Gi, int NoGi, int Class, int OpenMat, int Drilling, int Competition);
