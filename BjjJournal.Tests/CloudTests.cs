using System.Text;

namespace BjjJournal.Tests;

/// <summary>Partes de la sincronización que no necesitan red: fusión de copias y lectura de tokens.</summary>
public class CloudTests
{
    [Fact]
    public void Merge_SameItemEditedOnBoth_NewestWins()
    {
        var id = Guid.NewGuid();
        var local = new AppData { Techniques = [new Technique { Id = id, Name = "Local", UpdatedAt = DateTimeOffset.UtcNow }] };
        var remote = new AppData { Techniques = [new Technique { Id = id, Name = "Nube", UpdatedAt = DateTimeOffset.UtcNow.AddMinutes(1) }] };

        Assert.Equal("Nube", Cloud.Merge(local, remote).Techniques.Single().Name);
    }

    [Fact]
    public void Merge_DifferentItems_KeepsBoth_AndDeletionsTravel()
    {
        var deletedLater = new TrainingSession { IsDeleted = true, UpdatedAt = DateTimeOffset.UtcNow.AddMinutes(1) };
        var stillThereLocally = new TrainingSession { Id = deletedLater.Id, UpdatedAt = DateTimeOffset.UtcNow };
        var local = new AppData { Sessions = [new TrainingSession(), stillThereLocally] };
        var remote = new AppData { Sessions = [deletedLater], Profile = new UserProfile { DisplayName = "Ana" } };

        var merged = Cloud.Merge(local, remote);
        Assert.Equal(2, merged.Sessions.Count);
        Assert.True(merged.Sessions.Single(s => s.Id == deletedLater.Id).IsDeleted);
        Assert.Equal("Ana", merged.Profile!.DisplayName);
    }

    [Fact]
    public void ReadJwt_ReadsUserEmailAndExpiry()
    {
        var user = Guid.NewGuid();
        var payload = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{{\"sub\":\"{user}\",\"email\":\"a@b.com\",\"exp\":1700000000}}")).TrimEnd('=');

        Assert.Equal((user, "a@b.com", DateTimeOffset.FromUnixTimeSeconds(1700000000)), Cloud.ReadJwt($"x.{payload}.firma"));
        Assert.Null(Cloud.ReadJwt("no-es-un-token"));
    }

    [Fact]
    public void ParseFragment_ReadsGoogleRedirect()
    {
        var values = Cloud.ParseFragment("https://app/#access_token=abc&error_description=Algo+fue%20mal");
        Assert.Equal("abc", values["access_token"]);
        Assert.Equal("Algo+fue mal", values["error_description"]);
        Assert.Empty(Cloud.ParseFragment("https://app/"));
    }
}
