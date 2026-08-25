namespace MapPackBuilder.Sources;

internal enum SourceMapDisposition
{
    Existing,
    Variant,
    New
}

internal sealed record SourcePosition(double X, double Y, double Z);

internal sealed record TarkovDevExtract(
    string Id, string Name, string Faction, SourcePosition Position);

internal sealed record TarkovDevTransit(
    string Id, string Name, SourcePosition Position);

internal sealed record TarkovDevSpawn(
    string ZoneName,
    SourcePosition Position,
    IReadOnlyList<string> Sides,
    IReadOnlyList<string> Categories);

internal sealed record TarkovDevBossLocation(string Name, string SpawnKey);

internal sealed record TarkovDevBoss(
    string Name,
    IReadOnlyList<TarkovDevBossLocation> SpawnLocations);

internal sealed record TarkovDevHazard(
    string Id,
    string Name,
    SourcePosition Position,
    IReadOnlyList<SourcePosition> Outline);

internal sealed record TarkovDevMap(
    string UpstreamId,
    string MapId,
    string Name,
    SourceMapDisposition Disposition,
    double? ApiCardinalRotation,
    IReadOnlyList<TarkovDevExtract> Extracts,
    IReadOnlyList<TarkovDevTransit> Transits,
    IReadOnlyList<TarkovDevSpawn> Spawns,
    IReadOnlyList<TarkovDevBoss> Bosses,
    IReadOnlyList<TarkovDevHazard> Hazards);
