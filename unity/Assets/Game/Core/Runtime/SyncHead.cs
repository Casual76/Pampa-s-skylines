namespace PampaSkylines.Core
{
using System;

public sealed class SyncHead
{
    public int SchemaVersion { get; set; } = 4;

    public string CityId { get; set; } = string.Empty;

    public string Version { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string ClientId { get; set; } = "local";

    public long CommandCount { get; set; }

    public long Tick { get; set; }

    public DateTimeOffset ClientUpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public string Checksum { get; set; } = string.Empty;

    public static SyncHead FromSnapshot(CitySnapshot snapshot)
    {
        return new SyncHead
        {
            SchemaVersion = snapshot.SchemaVersion,
            CityId = snapshot.CityId,
            Version = snapshot.Version,
            DisplayName = snapshot.CityName,
            ClientId = snapshot.ClientId,
            CommandCount = snapshot.CommandCount,
            Tick = snapshot.State.Tick,
            ClientUpdatedAtUtc = snapshot.SavedAtUtc,
            Checksum = snapshot.ContentHash
        };
    }
}
}
