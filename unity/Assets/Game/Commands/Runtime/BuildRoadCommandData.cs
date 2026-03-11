#nullable enable

namespace PampaSkylines.Commands
{
using PampaSkylines.Shared;

public sealed class BuildRoadCommandData
{
    public string? RoadTypeId { get; set; }

    public Int2 Start { get; set; }

    public Int2 End { get; set; }

    public int Lanes { get; set; } = 2;
}
}
