using BjjJournal.Application.Dtos;
using BjjJournal.Application.Persistence;
using BjjJournal.Application.Services;
using BjjJournal.Application.Sync;
using BjjJournal.Domain.Entities;
using Shouldly;

namespace BjjJournal.Application.Tests;

public class SyncTests
{
    private static readonly DateTimeOffset T0 = new(2026, 11, 12, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Merge_SameEntityEditedOnBoth_NewestUpdatedAtWins()
    {
        var id = Guid.NewGuid();
        var local = new AppData { Techniques = [new Technique { Id = id, Name = "Kimura vieja", UpdatedAt = T0 }] };
        var remote = new AppData { Techniques = [new Technique { Id = id, Name = "Kimura nueva", UpdatedAt = T0.AddMinutes(5) }] };

        JournalMerger.Merge(local, remote).Techniques.Single().Name.ShouldBe("Kimura nueva");
        JournalMerger.Merge(remote, local).Techniques.Single().Name.ShouldBe("Kimura nueva");
    }

    [Fact]
    public void Merge_DifferentEntities_KeepsBoth()
    {
        var local = new AppData { Sessions = [new TrainingSession { DurationMinutes = 60 }] };
        var remote = new AppData { Sessions = [new TrainingSession { DurationMinutes = 90 }] };
        JournalMerger.Merge(local, remote).Sessions.Count.ShouldBe(2);
    }

    [Fact]
    public void Merge_DeletedRemotelyLater_StaysDeleted()
    {
        var id = Guid.NewGuid();
        var local = new AppData { Sessions = [new TrainingSession { Id = id, UpdatedAt = T0 }] };
        var remote = new AppData { Sessions = [new TrainingSession { Id = id, UpdatedAt = T0.AddMinutes(1), IsDeleted = true }] };
        JournalMerger.Merge(local, remote).Sessions.Single().IsDeleted.ShouldBeTrue();
    }

    [Fact]
    public void Merge_ProfileOnlyRemote_IsKept()
    {
        var remote = new AppData { Profile = new UserProfile { DisplayName = "Ana" } };
        JournalMerger.Merge(new AppData(), remote).Profile!.DisplayName.ShouldBe("Ana");
    }

    [Fact]
    public async Task SyncAsync_NotSignedIn_DoesNothing()
    {
        var (j, remote, _, sync) = Build();
        (await sync.SyncAsync()).IsSuccess.ShouldBeTrue();
        remote.Pushes.ShouldBe(0);
        j.Store.Writes.ShouldBe(0);
    }

    [Fact]
    public async Task SyncAsync_FirstLogin_UploadsLocalData()
    {
        var (j, remote, auth, sync) = Build();
        await j.Sessions.CreateAsync(SessionBuilder.At(j.Time.GetUtcNow()));
        await auth.SignInAsync("ana@test.com", "secreto1");

        await sync.SyncAsync();
        remote.Pushes.ShouldBe(1);
        AppDataSerializer.Deserialize(remote.Json!)!.Sessions.Count.ShouldBe(1);
    }

    [Fact]
    public async Task SyncAsync_RemoteHasData_MergesIntoLocal()
    {
        var (j, remote, auth, sync) = Build();
        var otherDevice = new TestJournal();
        await otherDevice.Profiles.SaveAsync(new ProfileInput { DisplayName = "Ana" });
        await otherDevice.Sessions.CreateAsync(SessionBuilder.At(otherDevice.Time.GetUtcNow()));
        remote.Json = otherDevice.Store.Json;
        await auth.SignInAsync("ana@test.com", "secreto1");

        await sync.SyncAsync();
        (await j.Profiles.IsOnboardedAsync()).ShouldBeTrue();
        (await j.Sessions.ListAsync(new SessionFilter())).Count.ShouldBe(1);
    }

    [Fact]
    public async Task SyncAsync_Offline_ReportsErrorAndKeepsLocal()
    {
        var (j, remote, auth, sync) = Build();
        await j.Sessions.CreateAsync(SessionBuilder.At(j.Time.GetUtcNow()));
        await auth.SignInAsync("ana@test.com", "secreto1");
        remote.Offline = true;

        var result = await sync.SyncAsync();
        result.IsSuccess.ShouldBeFalse();
        sync.LastError.ShouldNotBeNull();
        (await j.Sessions.ListAsync(new SessionFilter())).Count.ShouldBe(1);
    }

    [Fact]
    public async Task ClearLocalAsync_RemovesLocalData()
    {
        var (j, _, _, sync) = Build();
        await j.Sessions.CreateAsync(SessionBuilder.At(j.Time.GetUtcNow()));
        await sync.ClearLocalAsync();
        (await j.Sessions.ListAsync(new SessionFilter())).ShouldBeEmpty();
    }

    private static (TestJournal J, FakeRemoteStore Remote, AuthService Auth, SyncService Sync) Build()
    {
        var j = new TestJournal();
        var remote = new FakeRemoteStore();
        var auth = new AuthService(new FakeAuthApi(j.Time), new FakeKeyValueStore(), j.Time);
        return (j, remote, auth, new SyncService(j.Context, remote, auth, j.Time));
    }
}
