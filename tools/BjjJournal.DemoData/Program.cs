// Genera samples/demo-journal.json: un año de entrenamiento (3 clases + 1 open mat por semana).
// Uso: dotnet run --project tools/BjjJournal.DemoData -- samples/demo-journal.json
using BjjJournal.Application.Persistence;
using BjjJournal.Application.Seed;
using BjjJournal.Domain.Entities;
using BjjJournal.Domain.Enums;

var rnd = new Random(2025);
var madrid = TimeZoneInfo.FindSystemTimeZoneById("Europe/Madrid");
DateTimeOffset At(DateOnly d, int h, int m)
{
    var local = d.ToDateTime(new TimeOnly(h, m));
    return new DateTimeOffset(local, madrid.GetUtcOffset(local)).ToUniversalTime();
}

var start = new DateOnly(2025, 11, 10); // lunes
var end = new DateOnly(2026, 11, 11);
var t0 = At(start, 9, 0);

var data = new AppData();
SeedData.Apply(data, t0);
data.Profile = new UserProfile
{
    DisplayName = "Demo", Belt = Belt.White, Stripes = 3, GymName = "Club BJJ Demo",
    StartedTraining = start, CreatedAt = t0, UpdatedAt = t0,
};

var tech = data.Techniques.ToDictionary(t => t.Name);
var pos = data.Positions.ToDictionary(p => p.Name);
Technique T(string n) => tech[n];

// Bloques temáticos de dos semanas, como en un club real.
string[][] blocks =
[
    ["Recuperar guardia (shrimp)", "Upa (puente)", "Escape codo-rodilla"],
    ["Kimura", "Triángulo", "Armbar desde guardia", "Barrida de tijera"],
    ["Abrir guardia de rodillas", "Pase toreando", "Pase de rodilla cortada (knee cut)"],
    ["Control de cien kilos", "Americana", "Montada desde cien kilos", "Escape de cien kilos a guardia"],
    ["Barrida de cadera (hip bump)", "Guillotina", "Barrida de péndulo", "Cross collar choke"],
    ["Control de espalda (seatbelt)", "Mata león", "Tomar la espalda desde tortuga", "Escape de espalda"],
    ["Doble pierna", "Single leg", "O soto gari", "Tirarse a guardia"],
    ["Barrida old school", "Pase de media guardia", "Underhook a la espalda", "Darce"],
    ["Control de montada", "Armbar desde montada", "Ezequiel", "Rodilla en el pecho a montada"],
    ["Barrida de mariposa", "Barrida de gancho (hook sweep)", "Tripod sweep", "Levantada técnica"],
    ["Omoplata", "Flower sweep", "Defensa de triángulo", "Defensa de armbar"],
    ["Pase over-under", "Stack pass", "Body lock pass", "Pase leg drag"],
    ["Llave de tobillo recta", "Kimura desde norte-sur", "Bow and arrow", "Anaconda"],
    ["Barrida De La Riva", "Barrida de guardia X", "Granby roll", "Escape de rodilla en el pecho"],
];
string[] nogiOnly = ["Guillotina", "Darce", "Anaconda", "Llave de tobillo recta", "Single leg", "Doble pierna", "Body lock pass", "Mata león", "Barrida de mariposa"];

(string Name, Belt Belt)[] partners =
[
    ("Marc", Belt.White), ("Javi", Belt.White), ("Laura", Belt.White), ("Sergio", Belt.White), ("Pau", Belt.White),
    ("Álex", Belt.White), ("Nuria", Belt.Blue), ("Dani", Belt.Blue), ("Raúl", Belt.Blue), ("Iván", Belt.Blue),
    ("Carlos", Belt.Purple), ("Miguel", Belt.Purple), ("Toni", Belt.Brown), ("Profe Óscar", Belt.Black),
];

string[] subsFor = ["Kimura", "Triángulo", "Armbar desde guardia", "Guillotina", "Mata león", "Americana", "Cross collar choke", "Armbar desde montada", "Ezequiel", "Darce", "Omoplata", "Llave de tobillo recta", "Bow and arrow"];
string[] subsAgainst = ["Mata león", "Triángulo", "Armbar desde montada", "Kimura", "Guillotina", "Ezequiel", "Cross collar choke", "Americana", "Darce", "Bow and arrow", "Llave de tobillo recta", "Omoplata", "Anaconda"];
string[] sweeps = ["Barrida de tijera", "Barrida de cadera (hip bump)", "Barrida old school", "Barrida de péndulo", "Barrida de mariposa", "Tripod sweep", "Flower sweep"];
string[] passes = ["Pase toreando", "Pase de rodilla cortada (knee cut)", "Pase de media guardia", "Pase over-under", "Stack pass", "Body lock pass"];
(string Tech, string Pos)[] escapes = [("Upa (puente)", "Montada (abajo)"), ("Escape codo-rodilla", "Montada (abajo)"), ("Escape de cien kilos a guardia", "Cien kilos (abajo)"), ("Escape de espalda", "Espalda (defendiendo)"), ("Recuperar guardia (shrimp)", "Cien kilos (abajo)"), ("Granby roll", "Tortuga")];

var known = new HashSet<string>();
var usage = new Dictionary<string, int>();
var unlockedSubs = new List<string> { "Kimura", "Americana" };
void Use(string n, int w = 1) => usage[n] = usage.GetValueOrDefault(n) + w;

T Pick<T>(IList<T> list, double bias = 0) => list[Math.Min(list.Count - 1, (int)(Math.Pow(rnd.NextDouble(), 1 + bias) * list.Count))];

bool Holiday(DateOnly d) =>
    (d >= new DateOnly(2025, 12, 22) && d <= new DateOnly(2026, 1, 6)) ||
    (d >= new DateOnly(2026, 3, 30) && d <= new DateOnly(2026, 4, 5)) ||
    (d >= new DateOnly(2026, 8, 3) && d <= new DateOnly(2026, 8, 23));

string[] classNotes = ["Me ha costado el timing, repetir el viernes.", "Por fin me sale fluido.", "Cuidado con dejar el brazo dentro.", "El profe insiste en los agarres antes de moverse.", "Cansado, pero buena clase.", "Hoy he entendido la mecánica de la cadera.", "Mucho drilling, pocos rolls."];
string[] matNotes = ["Rolls muy duros con los azules.", "He conseguido aplicar lo de la semana.", "Me han pasado la guardia demasiadas veces.", "Mejor gas que el mes pasado.", "Buen ambiente, muchos rolls técnicos.", "Me han finalizado poco, voy mejorando la defensa."];

Roll MakeRoll(Guid sessionId, DateOnly d, DateTimeOffset stamp, int minutes, double p, int maxEvents)
{
    var (name, belt) = partners[rnd.Next(partners.Length)];
    var roll = new Roll { SessionId = sessionId, PartnerName = name, PartnerBelt = belt, DurationMinutes = minutes, CreatedAt = stamp, UpdatedAt = stamp };
    var gap = (int)belt - 0.3 - p * 1.3; // >0: el compañero es claramente mejor
    var n = rnd.Next(1, maxEvents + 1);
    for (var i = 0; i < n; i++)
    {
        var r = rnd.NextDouble();
        double pFor = Math.Clamp(0.10 + 0.25 * p - 0.08 * gap, 0.02, 0.45);
        double pAgainst = Math.Clamp(0.30 - 0.18 * p + 0.10 * gap, 0.05, 0.65);
        double pSweep = 0.17 + 0.08 * p, pPass = 0.10 + 0.10 * p;
        RollEvent e;
        if (r < pFor)
        {
            var s = Pick(unlockedSubs);
            e = new RollEvent { Kind = RollEventKind.SubmissionFor, TechniqueId = T(s).Id, PositionId = T(s).PositionId };
            Use(s, 2);
        }
        else if (r < pFor + pAgainst)
        {
            var s = Pick(subsAgainst, 0.6);
            e = new RollEvent { Kind = RollEventKind.SubmissionAgainst, TechniqueId = T(s).Id, PositionId = T(s).PositionId };
        }
        else if (r < pFor + pAgainst + pSweep)
        {
            var opts = sweeps.Where(known.Contains).ToList();
            if (opts.Count == 0) continue;
            var s = Pick(opts);
            e = new RollEvent { Kind = RollEventKind.SweepFor, TechniqueId = T(s).Id, PositionId = T(s).PositionId };
            Use(s, 2);
        }
        else if (r < pFor + pAgainst + pSweep + pPass)
        {
            var opts = passes.Where(known.Contains).ToList();
            if (opts.Count == 0) continue;
            var s = Pick(opts);
            e = new RollEvent { Kind = RollEventKind.PassFor, TechniqueId = T(s).Id, PositionId = T(s).PositionId };
            Use(s, 2);
        }
        else
        {
            var opts = escapes.Where(x => known.Contains(x.Tech)).ToList();
            if (opts.Count == 0) continue;
            var (s, ps) = Pick(opts);
            e = new RollEvent { Kind = RollEventKind.Escape, TechniqueId = T(s).Id, PositionId = pos[ps].Id };
            Use(s, 2);
        }
        e = new RollEvent { Kind = e.Kind, TechniqueId = e.TechniqueId, PositionId = e.PositionId, RollId = roll.Id, CreatedAt = stamp, UpdatedAt = stamp };
        roll.Events.Add(e);
    }
    return roll;
}

var weekIndex = 0;
for (var monday = start; monday <= end; monday = monday.AddDays(7), weekIndex++)
{
    var block = blocks[(weekIndex / 2) % blocks.Length];
    foreach (var (offset, kind) in new[] { (0, "gi"), (2, "nogi"), (4, "gi"), (5, "mat") })
    {
        var d = monday.AddDays(offset);
        if (d > end || Holiday(d) || rnd.NextDouble() < 0.05) continue;
        var p = (d.DayNumber - start.DayNumber) / 366.0;
        var isMat = kind == "mat";
        var attire = kind == "nogi" ? Attire.NoGi : isMat ? (rnd.NextDouble() < 0.6 ? Attire.Gi : Attire.NoGi) : Attire.Gi;
        var startAt = isMat ? At(d, 11, 0) : At(d, kind == "nogi" ? 20 : 19, 30);
        var duration = isMat ? rnd.Next(4, 8) * 15 : (kind == "nogi" ? 75 : 90);
        var stamp = startAt.AddMinutes(duration + rnd.Next(20, 180));
        var s = new TrainingSession
        {
            StartedAt = startAt, DurationMinutes = duration, Attire = attire,
            Type = isMat ? SessionType.OpenMat : SessionType.Class,
            Location = "Club BJJ Demo", CreatedAt = stamp, UpdatedAt = stamp,
        };

        if (!isMat)
        {
            var pool = block.Where(n => attire == Attire.NoGi || !nogiOnly.Contains(n) || rnd.NextDouble() < 0.5)
                .Where(n => attire == Attire.Gi || !T(n).Tags.Contains("gi")).OrderBy(_ => rnd.Next()).Take(rnd.Next(2, 4)).ToList();
            if (pool.Count == 0) pool = [block[0]];
            foreach (var n in pool)
            {
                var focus = known.Add(n) ? TechniqueFocus.Learned : TechniqueFocus.Drilled;
                s.Techniques.Add(new SessionTechnique { SessionId = s.Id, TechniqueId = T(n).Id, Focus = focus });
                Use(n);
                if (subsFor.Contains(n) && !unlockedSubs.Contains(n) && (usage[n] >= 2)) unlockedSubs.Add(n);
            }
            var rolls = rnd.Next(2, 4);
            for (var i = 0; i < rolls; i++) s.Rolls.Add(MakeRoll(s.Id, d, stamp, rnd.Next(5, 7), p, 2));
        }
        else
        {
            var rolls = rnd.Next(5, 8);
            for (var i = 0; i < rolls; i++) s.Rolls.Add(MakeRoll(s.Id, d, stamp, rnd.Next(6, 9), p, 3));
            var used = s.Rolls.SelectMany(r => r.Events)
                .Where(e => e.Kind != RollEventKind.SubmissionAgainst && e.TechniqueId is not null)
                .Select(e => e.TechniqueId!.Value).Distinct().Take(4);
            foreach (var id in used) s.Techniques.Add(new SessionTechnique { SessionId = s.Id, TechniqueId = id, Focus = TechniqueFocus.UsedInRoll });
        }

        var subF = s.Rolls.SelectMany(r => r.Events).Count(e => e.Kind == RollEventKind.SubmissionFor);
        var subA = s.Rolls.SelectMany(r => r.Events).Count(e => e.Kind == RollEventKind.SubmissionAgainst);
        s.Satisfaction = Math.Clamp(3 + (subF > subA ? 1 : 0) - (subA - subF > 2 ? 1 : 0) + (rnd.NextDouble() < 0.25 ? 1 : 0) - (rnd.NextDouble() < 0.12 ? 1 : 0), 1, 5);
        if (rnd.NextDouble() < 0.3) s.Notes = isMat ? Pick(matNotes) : Pick(classNotes);
        data.Sessions.Add(s);
    }
}

var endStamp = At(end, 22, 0);
foreach (var t in data.Techniques)
{
    var u = usage.GetValueOrDefault(t.Name);
    var status = u == 0 ? ProgressStatus.NotStarted : u < 8 ? ProgressStatus.Learning : u < 60 ? ProgressStatus.Competent : ProgressStatus.Mastered;
    if (status != t.Progress) { t.Progress = status; t.UpdatedAt = endStamp; }
}

var json = AppDataSerializer.Serialize(data);
File.WriteAllText(args[0], json);
var ev = data.Sessions.SelectMany(s => s.Rolls).SelectMany(r => r.Events).ToList();
Console.WriteLine($"sesiones={data.Sessions.Count} clases={data.Sessions.Count(s => s.Type == SessionType.Class)} openmat={data.Sessions.Count(s => s.Type == SessionType.OpenMat)} rolls={data.Sessions.Sum(s => s.Rolls.Count)} eventos={ev.Count} subsFor={ev.Count(e => e.Kind == RollEventKind.SubmissionFor)} subsAgainst={ev.Count(e => e.Kind == RollEventKind.SubmissionAgainst)} horas={data.Sessions.Sum(s => s.DurationMinutes) / 60} bytes={json.Length}");
foreach (var g in data.Techniques.GroupBy(t => t.Progress)) Console.WriteLine($"{g.Key}: {g.Count()}");
