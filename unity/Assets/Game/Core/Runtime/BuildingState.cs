namespace PampaSkylines.Core
{
using PampaSkylines.Shared;

public sealed class BuildingState
{
    public string Id { get; set; } = string.Empty;

    public string LotId { get; set; } = string.Empty;

    public Int2 Cell { get; set; }

    public ZoneType ZoneType { get; set; }

    public ServiceType ServiceType { get; set; }

    public int Level { get; set; } = 1;

    public int Residents { get; set; }

    public int Jobs { get; set; }

    public float Condition { get; set; } = 1f;

    public float CoverageRadius { get; set; } = 4f;

    public float DistrictVitality { get; set; } = 0.5f;
}
}
