namespace HunterPie.Features.Statistics.Models;

/// <summary>
/// Weapon-stats snapshot (raw / element / affinity) taken at quest start.
/// Fingerprints the exact weapon: the fork exports no weapon names or ids.
/// Null for party members (their gear stats are not memory-mapped).
/// </summary>
internal record GearStatusModel(
    double Raw,
    double Element,
    double Affinity
);
