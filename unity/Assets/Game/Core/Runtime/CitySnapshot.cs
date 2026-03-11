#nullable enable

namespace PampaSkylines.Core
{
using System;

public sealed class CitySnapshot
{
    public int SchemaVersion { get; set; } = 4;

    public string CityId { get; set; } = string.Empty;

    public string CityName { get; set; } = string.Empty;

    public string Version { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset SavedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public string ClientId { get; set; } = "local";

    public long CommandCount { get; set; }

    public string ContentHash { get; set; } = string.Empty;

    public SnapshotMetadata Metadata { get; set; } = new();

    public WorldState State { get; set; } = new();

    public CitySnapshot DeepClone()
    {
        return PampaSkylinesClone.DeepCopy(this);
    }

    public static CitySnapshot FromWorld(WorldState state, string version, string clientId = "local", SnapshotMetadata? metadata = null)
    {
        var snapshot = new CitySnapshot
        {
            CityId = state.CityId,
            CityName = state.CityName,
            Version = version,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            SavedAtUtc = DateTimeOffset.UtcNow,
            ClientId = clientId,
            CommandCount = state.AppliedCommandCount,
            Metadata = metadata ?? new SnapshotMetadata
            {
                SourceClientId = clientId
            },
            State = PampaSkylinesClone.DeepCopy(state)
        };

        snapshot.ContentHash = SnapshotHashing.ComputeContentHash(snapshot);
        return snapshot;
    }
}
}
