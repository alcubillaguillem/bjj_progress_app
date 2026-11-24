namespace BjjJournal.Tests;

/// <summary>Guardar, validar, buscar e importar en el diario (sin navegador: el diario vive en memoria).</summary>
public class JournalTests
{
    [Fact]
    public async Task Technique_EmptyName_IsRejected()
    {
        var journal = new Journal();
        Assert.Equal("El nombre de la técnica es obligatorio.", await journal.SaveTechniqueAsync(new Technique { Name = " " }));
        Assert.Empty(journal.Techniques);
    }

    [Fact]
    public async Task Technique_SameNameAndPositionIgnoringAccents_IsRejected()
    {
        var journal = new Journal();
        var position = await journal.AddPositionAsync("Guardia cerrada");
        await journal.SaveTechniqueAsync(new Technique { Name = "Kimura", PositionId = position.Id });

        Assert.NotNull(await journal.SaveTechniqueAsync(new Technique { Name = "kímura", PositionId = position.Id }));
        Assert.Null(await journal.SaveTechniqueAsync(new Technique { Name = "Kimura" })); // otra posición: vale
    }

    [Fact]
    public async Task Technique_TagsAndLinks_AreCleaned()
    {
        var journal = new Journal();
        var t = new Technique { Name = " Triángulo ", Tags = ["#gi", "gi", " ", "presión"], Notes = "  " };
        await journal.SaveTechniqueAsync(t);

        Assert.Equal("Triángulo", t.Name);
        Assert.Equal(["gi", "presión"], t.Tags);
        Assert.Null(t.Notes);
    }

    [Fact]
    public async Task Search_FindsByTagNotesOrPosition_WithoutAccents()
    {
        var journal = new Journal();
        var position = await journal.AddPositionAsync("Espalda");
        await journal.SaveTechniqueAsync(new Technique { Name = "Mata león", PositionId = position.Id, Tags = ["estrangulación"] });
        await journal.SaveTechniqueAsync(new Technique { Name = "Kimura", Notes = "Agarre en figura 4" });

        Assert.Single(journal.Search("ESTRANGULACION"));
        Assert.Single(journal.Search("espalda"));
        Assert.Single(journal.Search("figura"));
        Assert.Equal(2, journal.Search("").Count());
    }

    [Theory]
    [InlineData(0, 3, "La duración debe ser mayor que 0 minutos.")]
    [InlineData(60, 6, "La satisfacción va de 1 a 5.")]
    public async Task Session_InvalidValues_AreRejected(int minutes, int satisfaction, string error)
    {
        var journal = new Journal();
        Assert.Equal(error, await journal.SaveSessionAsync(new TrainingSession { DurationMinutes = minutes, Satisfaction = satisfaction }));
        Assert.Empty(journal.Sessions);
    }

    [Fact]
    public async Task Session_SameTechniqueTwice_IsStoredOnce()
    {
        var journal = new Journal();
        var id = Guid.NewGuid();
        var s = new TrainingSession { Techniques = [new() { TechniqueId = id }, new() { TechniqueId = id }] };
        await journal.SaveSessionAsync(s);
        Assert.Single(journal.Sessions.Single().Techniques);
    }

    [Fact]
    public async Task Delete_TechniqueInSession_KeepsItsNameInHistory()
    {
        var journal = new Journal();
        var t = new Technique { Name = "Omoplata" };
        await journal.SaveTechniqueAsync(t);
        await journal.SaveSessionAsync(new TrainingSession { Techniques = [new() { TechniqueId = t.Id }] });

        await journal.DeleteAsync(t);

        Assert.Empty(journal.Techniques);
        Assert.Equal("Omoplata", journal.TechniqueName(t.Id));
    }

    [Fact]
    public async Task NewSession_CopiesLastDurationTypeAndAttire()
    {
        var journal = new Journal();
        Assert.Equal(90, journal.NewSession().DurationMinutes);

        await journal.SaveSessionAsync(new TrainingSession { DurationMinutes = 60, Type = SessionType.OpenMat, Attire = Attire.NoGi });
        var next = journal.NewSession(new DateOnly(2026, 1, 5));

        Assert.Equal((60, SessionType.OpenMat, Attire.NoGi), (next.DurationMinutes, next.Type, next.Attire));
        Assert.Equal(new DateOnly(2026, 1, 5), next.Day);
    }

    [Fact]
    public async Task Profile_FiveStripes_IsRejected_AndSeedLoadsTechniques()
    {
        var journal = new Journal();
        Assert.Equal("Los grados van de 0 a 4.", await journal.SaveProfileAsync(new UserProfile { DisplayName = "Ana", Stripes = 5 }));

        Assert.Null(await journal.SaveProfileAsync(new UserProfile { DisplayName = "Ana" }, loadSeed: true));
        Assert.NotEmpty(journal.Techniques);
        Assert.All(journal.Techniques, t => Assert.NotNull(journal.PositionName(t.PositionId)));
    }

    [Fact]
    public async Task Backup_ExportThenRead_KeepsEverything()
    {
        var journal = new Journal();
        await journal.SaveProfileAsync(new UserProfile { DisplayName = "Ana" }, loadSeed: true);
        await journal.SaveSessionAsync(new TrainingSession { Rolls = [new() { Events = [new() { Kind = RollEventKind.SweepFor }] }] });

        Assert.Null(Journal.ReadBackup(journal.Export(), out var copy));
        Assert.Equal(journal.Data.Techniques.Count, copy.Techniques.Count);
        Assert.Equal(RollEventKind.SweepFor, copy.Sessions.Single().Events.Single().Kind);
    }

    [Theory]
    [InlineData("no es json", "El fichero no es un JSON válido de BJJ Journal.")]
    [InlineData("{\"schemaVersion\": 99}", "Versión de fichero no soportada (99).")]
    public void Backup_BadFile_ReturnsError(string json, string error) =>
        Assert.Equal(error, Journal.ReadBackup(json, out _));

    [Fact]
    public async Task Import_Merge_ReusesDuplicateTechniquesAndSkipsRepeatedSessions()
    {
        var journal = new Journal();
        var mine = new Technique { Name = "Kimura" };
        await journal.SaveTechniqueAsync(mine);

        var theirs = new Technique { Name = "kimura" };
        var file = new AppData
        {
            Techniques = [theirs, new Technique { Name = "Triángulo" }],
            Sessions = [new TrainingSession { Techniques = [new() { TechniqueId = theirs.Id }] }],
        };
        Assert.Single(journal.DuplicateTechniques(file));

        await journal.ImportAsync(Journal.Copy(file), replace: false);
        await journal.ImportAsync(Journal.Copy(file), replace: false);

        Assert.Equal(["Kimura", "Triángulo"], journal.Techniques.Select(t => t.Name));
        Assert.Equal(mine.Id, journal.Sessions.Single().Techniques.Single().TechniqueId);
    }

    [Fact]
    public async Task Import_Replace_ReplacesEverything()
    {
        var journal = new Journal();
        await journal.SaveTechniqueAsync(new Technique { Name = "Kimura" });
        await journal.ImportAsync(new AppData(), replace: true);
        Assert.Empty(journal.Techniques);
    }
}
