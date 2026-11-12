using BjjJournal.Application.Dtos;
using BjjJournal.Application.Persistence;
using BjjJournal.Application.Seed;
using BjjJournal.Domain.Common;
using BjjJournal.Domain.Entities;
using BjjJournal.Domain.Rules;

namespace BjjJournal.Application.Services;

public sealed class ProfileService(JournalContext context, TimeProvider time)
{
    public async Task<bool> IsOnboardedAsync(CancellationToken ct = default) =>
        (await context.GetAsync(ct)).Profile is not null;

    public async Task<ProfileInput?> GetAsync(CancellationToken ct = default)
    {
        var p = (await context.GetAsync(ct)).Profile;
        return p is null
            ? null
            : new ProfileInput
            {
                DisplayName = p.DisplayName,
                Belt = p.Belt,
                Stripes = p.Stripes,
                GymName = p.GymName,
                StartedTraining = p.StartedTraining,
            };
    }

    public async Task<Result> SaveAsync(ProfileInput input, CancellationToken ct = default)
    {
        var data = await context.GetAsync(ct);
        var now = time.GetUtcNow();
        var profile = data.Profile ?? new UserProfile { CreatedAt = now };
        profile.DisplayName = input.DisplayName.Trim();
        profile.Belt = input.Belt;
        profile.Stripes = input.Stripes;
        profile.GymName = Projections.CleanText(input.GymName);
        profile.StartedTraining = input.StartedTraining;
        profile.UpdatedAt = now;

        var validation = DomainRules.Validate(profile);
        if (!validation.IsSuccess)
        {
            return validation;
        }

        data.Profile = profile;
        await context.SaveAsync(ct);
        return Result.Ok();
    }

    /// <summary>Último paso del onboarding: guarda el perfil y, si se pide, carga la semilla de posiciones y técnicas.</summary>
    public async Task<Result> CompleteOnboardingAsync(ProfileInput input, bool loadSeed, CancellationToken ct = default)
    {
        var data = await context.GetAsync(ct);
        if (loadSeed && data.Techniques.Count == 0)
        {
            SeedData.Apply(data, time.GetUtcNow());
        }

        return await SaveAsync(input, ct);
    }
}
