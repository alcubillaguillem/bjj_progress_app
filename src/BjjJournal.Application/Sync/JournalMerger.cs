using BjjJournal.Application.Persistence;
using BjjJournal.Domain.Common;

namespace BjjJournal.Application.Sync;

/// <summary>
/// Fusiona dos copias del diario entidad a entidad: gana el UpdatedAt más reciente (last-write-wins).
/// Los borrados lógicos (IsDeleted) viajan como cualquier otro cambio.
/// </summary>
public static class JournalMerger
{
    public static AppData Merge(AppData local, AppData remote) => new()
    {
        SchemaVersion = AppData.CurrentSchemaVersion,
        Profile = Newest(local.Profile, remote.Profile),
        Positions = MergeList(local.Positions, remote.Positions),
        Techniques = MergeList(local.Techniques, remote.Techniques),
        Links = MergeList(local.Links, remote.Links),
        Sessions = MergeList(local.Sessions, remote.Sessions),
    };

    private static T? Newest<T>(T? a, T? b)
        where T : Entity =>
        (a, b) switch
        {
            (null, _) => b,
            (_, null) => a,
            _ => b.UpdatedAt > a.UpdatedAt ? b : a,
        };

    private static List<T> MergeList<T>(List<T> local, List<T> remote)
        where T : Entity
    {
        var byId = new Dictionary<Guid, T>();
        var order = new List<Guid>();
        foreach (var item in local.Concat(remote))
        {
            if (byId.TryGetValue(item.Id, out var existing))
            {
                byId[item.Id] = Newest(existing, item)!;
            }
            else
            {
                byId[item.Id] = item;
                order.Add(item.Id);
            }
        }

        return order.Select(id => byId[id]).ToList();
    }
}
