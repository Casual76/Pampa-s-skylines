#nullable enable

namespace PampaSkylines.Core
{
public sealed class SnapshotMetadata
{
    public string SourceClientId { get; set; } = "local";

    public string SourcePlatform { get; set; } = "unknown";

    public string SimulationConfigVersion { get; set; } = "unknown";

    public string DebugLabel { get; set; } = string.Empty;

    public string SaveSlotId { get; set; } = "autosave";

    public string SaveReason { get; set; } = "manual";

    public string LastSyncStatus { get; set; } = "pending";
}
}
