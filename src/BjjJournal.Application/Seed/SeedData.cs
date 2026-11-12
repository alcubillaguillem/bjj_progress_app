using BjjJournal.Application.Persistence;
using BjjJournal.Domain.Entities;
using BjjJournal.Domain.Enums;

namespace BjjJournal.Application.Seed;

/// <summary>Semilla inicial: posiciones y técnicas básicas, con plantilla de temario para blanco y azul. El usuario lo edita todo.</summary>
public static class SeedData
{
    private static readonly (string Name, PositionRole Role)[] Positions =
    [
        ("De pie", PositionRole.Neutral),
        ("Guardia cerrada", PositionRole.Bottom),
        ("Dentro de la guardia cerrada", PositionRole.Top),
        ("Media guardia", PositionRole.Bottom),
        ("Media guardia (arriba)", PositionRole.Top),
        ("Guardia abierta", PositionRole.Bottom),
        ("Guardia mariposa", PositionRole.Bottom),
        ("Guardia De La Riva", PositionRole.Bottom),
        ("Guardia De La Riva invertida", PositionRole.Bottom),
        ("Guardia araña", PositionRole.Bottom),
        ("Guardia lazo", PositionRole.Bottom),
        ("Guardia X", PositionRole.Bottom),
        ("Single leg X", PositionRole.Bottom),
        ("Guardia sentada", PositionRole.Bottom),
        ("Z-guard", PositionRole.Bottom),
        ("Guardia de rodilla escudo", PositionRole.Bottom),
        ("Rubber guard", PositionRole.Bottom),
        ("Montada", PositionRole.Top),
        ("Montada (abajo)", PositionRole.Bottom),
        ("Montada técnica", PositionRole.Top),
        ("Montada en S", PositionRole.Top),
        ("Cien kilos", PositionRole.Top),
        ("Cien kilos (abajo)", PositionRole.Bottom),
        ("Rodilla en el pecho", PositionRole.Top),
        ("Rodilla en el pecho (abajo)", PositionRole.Bottom),
        ("Norte-sur", PositionRole.Top),
        ("Kesa gatame", PositionRole.Top),
        ("Espalda", PositionRole.Top),
        ("Espalda (defendiendo)", PositionRole.Bottom),
        ("Tortuga (atacando)", PositionRole.Top),
        ("Tortuga", PositionRole.Bottom),
        ("Crucifijo", PositionRole.Top),
        ("Ashi garami", PositionRole.Neutral),
        ("Saddle / 411", PositionRole.Neutral),
        ("50/50", PositionRole.Neutral),
        ("Clinch", PositionRole.Neutral),
        ("Agarres de solapa y manga", PositionRole.Neutral),
        ("Combate de manos", PositionRole.Neutral),
        ("Leg drag", PositionRole.Top),
        ("Body lock", PositionRole.Top),
    ];

    private static readonly (string Name, TechniqueCategory Category, string Position, Belt Belt, string[] Tags)[] Techniques =
    [
        ("Doble pierna", TechniqueCategory.Takedown, "De pie", Belt.White, ["wrestling"]),
        ("Single leg", TechniqueCategory.Takedown, "De pie", Belt.White, ["wrestling"]),
        ("O soto gari", TechniqueCategory.Takedown, "Clinch", Belt.White, ["judo"]),
        ("Tirarse a guardia", TechniqueCategory.Guard, "De pie", Belt.White, []),
        ("Ippon seoi nage", TechniqueCategory.Takedown, "Agarres de solapa y manga", Belt.Blue, ["judo", "gi"]),
        ("Snap down a frontal", TechniqueCategory.Takedown, "De pie", Belt.Blue, ["wrestling"]),
        ("Kimura", TechniqueCategory.Submission, "Guardia cerrada", Belt.White, ["brazo"]),
        ("Triángulo", TechniqueCategory.Submission, "Guardia cerrada", Belt.White, ["estrangulación"]),
        ("Armbar desde guardia", TechniqueCategory.Submission, "Guardia cerrada", Belt.White, ["brazo"]),
        ("Guillotina", TechniqueCategory.Submission, "Guardia cerrada", Belt.White, ["estrangulación"]),
        ("Cross collar choke", TechniqueCategory.Submission, "Guardia cerrada", Belt.White, ["estrangulación", "gi"]),
        ("Omoplata", TechniqueCategory.Submission, "Guardia cerrada", Belt.Blue, ["hombro"]),
        ("Barrida de tijera", TechniqueCategory.Sweep, "Guardia cerrada", Belt.White, []),
        ("Barrida de cadera (hip bump)", TechniqueCategory.Sweep, "Guardia cerrada", Belt.White, []),
        ("Barrida de péndulo", TechniqueCategory.Sweep, "Guardia cerrada", Belt.White, ["gi"]),
        ("Flower sweep", TechniqueCategory.Sweep, "Guardia cerrada", Belt.Blue, ["gi"]),
        ("Barrida de mariposa", TechniqueCategory.Sweep, "Guardia mariposa", Belt.White, []),
        ("Barrida de gancho (hook sweep)", TechniqueCategory.Sweep, "Guardia mariposa", Belt.Blue, []),
        ("Barrida old school", TechniqueCategory.Sweep, "Media guardia", Belt.White, []),
        ("Underhook a la espalda", TechniqueCategory.Transition, "Media guardia", Belt.Blue, []),
        ("Barrida De La Riva", TechniqueCategory.Sweep, "Guardia De La Riva", Belt.Blue, []),
        ("Berimbolo", TechniqueCategory.Transition, "Guardia De La Riva", Belt.Purple, []),
        ("Barrida de guardia X", TechniqueCategory.Sweep, "Guardia X", Belt.Blue, []),
        ("Tripod sweep", TechniqueCategory.Sweep, "Guardia abierta", Belt.White, []),
        ("Barrida de guardia araña", TechniqueCategory.Sweep, "Guardia araña", Belt.Blue, ["gi"]),
        ("Abrir guardia de rodillas", TechniqueCategory.Pass, "Dentro de la guardia cerrada", Belt.White, []),
        ("Abrir guardia de pie", TechniqueCategory.Pass, "Dentro de la guardia cerrada", Belt.White, []),
        ("Pase toreando", TechniqueCategory.Pass, "Guardia abierta", Belt.White, []),
        ("Pase de rodilla cortada (knee cut)", TechniqueCategory.Pass, "Guardia abierta", Belt.White, []),
        ("Pase over-under", TechniqueCategory.Pass, "Guardia abierta", Belt.Blue, ["presión"]),
        ("Pase leg drag", TechniqueCategory.Pass, "Leg drag", Belt.Blue, []),
        ("Pase de media guardia", TechniqueCategory.Pass, "Media guardia (arriba)", Belt.White, ["presión"]),
        ("Stack pass", TechniqueCategory.Pass, "Guardia cerrada", Belt.Blue, ["presión"]),
        ("Body lock pass", TechniqueCategory.Pass, "Body lock", Belt.Blue, ["presión"]),
        ("Americana", TechniqueCategory.Submission, "Cien kilos", Belt.White, ["brazo"]),
        ("Armbar desde montada", TechniqueCategory.Submission, "Montada", Belt.White, ["brazo"]),
        ("Ezequiel", TechniqueCategory.Submission, "Montada", Belt.Blue, ["estrangulación", "gi"]),
        ("Triángulo de brazo", TechniqueCategory.Submission, "Montada", Belt.Blue, ["estrangulación"]),
        ("Mata león", TechniqueCategory.Submission, "Espalda", Belt.White, ["estrangulación"]),
        ("Bow and arrow", TechniqueCategory.Submission, "Espalda", Belt.Blue, ["estrangulación", "gi"]),
        ("Kimura desde norte-sur", TechniqueCategory.Submission, "Norte-sur", Belt.Blue, ["brazo"]),
        ("Darce", TechniqueCategory.Submission, "Media guardia (arriba)", Belt.Blue, ["estrangulación"]),
        ("Anaconda", TechniqueCategory.Submission, "Tortuga (atacando)", Belt.Blue, ["estrangulación"]),
        ("Llave de tobillo recta", TechniqueCategory.Submission, "Ashi garami", Belt.White, ["piernas"]),
        ("Heel hook interior", TechniqueCategory.Submission, "Saddle / 411", Belt.Brown, ["piernas", "nogi"]),
        ("Kneebar", TechniqueCategory.Submission, "Ashi garami", Belt.Purple, ["piernas"]),
        ("Montada desde cien kilos", TechniqueCategory.Transition, "Cien kilos", Belt.White, []),
        ("Rodilla en el pecho a montada", TechniqueCategory.Transition, "Rodilla en el pecho", Belt.White, []),
        ("Tomar la espalda desde tortuga", TechniqueCategory.Transition, "Tortuga (atacando)", Belt.White, []),
        ("Control de cien kilos", TechniqueCategory.Control, "Cien kilos", Belt.White, ["presión"]),
        ("Control de montada", TechniqueCategory.Control, "Montada", Belt.White, []),
        ("Control de espalda (seatbelt)", TechniqueCategory.Control, "Espalda", Belt.White, []),
        ("Upa (puente)", TechniqueCategory.Escape, "Montada (abajo)", Belt.White, []),
        ("Escape codo-rodilla", TechniqueCategory.Escape, "Montada (abajo)", Belt.White, []),
        ("Escape de cien kilos a guardia", TechniqueCategory.Escape, "Cien kilos (abajo)", Belt.White, []),
        ("Escape de espalda", TechniqueCategory.Escape, "Espalda (defendiendo)", Belt.White, []),
        ("Escape de rodilla en el pecho", TechniqueCategory.Escape, "Rodilla en el pecho (abajo)", Belt.Blue, []),
        ("Granby roll", TechniqueCategory.Escape, "Tortuga", Belt.Blue, []),
        ("Defensa de triángulo", TechniqueCategory.Defense, "Dentro de la guardia cerrada", Belt.White, []),
        ("Defensa de armbar", TechniqueCategory.Defense, "Dentro de la guardia cerrada", Belt.White, []),
        ("Defensa de guillotina", TechniqueCategory.Defense, "De pie", Belt.Blue, []),
        ("Recuperar guardia (shrimp)", TechniqueCategory.Guard, "Cien kilos (abajo)", Belt.White, []),
        ("Levantada técnica", TechniqueCategory.Guard, "Guardia sentada", Belt.White, []),
    ];

    public static int PositionCount => Positions.Length;

    public static int TechniqueCount => Techniques.Length;

    public static void Apply(AppData data, DateTimeOffset now)
    {
        var positions = new Dictionary<string, Position>(StringComparer.OrdinalIgnoreCase);
        foreach (var (name, role) in Positions)
        {
            var p = data.Positions.FirstOrDefault(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase));
            if (p is null)
            {
                p = new Position { Name = name, Role = role, CreatedAt = now, UpdatedAt = now };
                data.Positions.Add(p);
            }

            positions[name] = p;
        }

        foreach (var (name, category, position, belt, tags) in Techniques)
        {
            data.Techniques.Add(new Technique
            {
                Name = name,
                Category = category,
                PositionId = positions[position].Id,
                CurriculumBelt = belt is Belt.White or Belt.Blue ? belt : null,
                Progress = ProgressStatus.NotStarted,
                Tags = [.. tags],
                CreatedAt = now,
                UpdatedAt = now,
            });
        }
    }
}
