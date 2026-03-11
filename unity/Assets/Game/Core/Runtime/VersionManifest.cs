namespace PampaSkylines.Core
{
using System;

public sealed class VersionManifest
{
    public string BackendApiVersion { get; set; } = "v1";

    public int MinimumSnapshotSchemaVersion { get; set; } = 1;

    public string WindowsBuild { get; set; } = "0.1.0-alpha";

    public string AndroidBuild { get; set; } = "0.1.0-alpha";

    public string SimulationConfigVersion { get; set; } = "v1";

    public DateTimeOffset GeneratedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
}
