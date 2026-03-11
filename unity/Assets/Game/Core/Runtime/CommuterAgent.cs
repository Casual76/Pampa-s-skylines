#nullable enable

namespace PampaSkylines.Core
{
public sealed class CommuterAgent
{
    public string Id { get; set; } = string.Empty;

    public string HomeBuildingId { get; set; } = string.Empty;

    public string WorkBuildingId { get; set; } = string.Empty;

    public string? CurrentRoadSegmentId { get; set; }

    public float CommuteMinutes { get; set; }
}
}
