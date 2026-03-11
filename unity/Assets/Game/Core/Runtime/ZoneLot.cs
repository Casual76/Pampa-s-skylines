#nullable enable

namespace PampaSkylines.Core
{
using PampaSkylines.Shared;

public sealed class ZoneLot
{
    public string Id { get; set; } = string.Empty;

    public Int2 Cell { get; set; }

    public ZoneType ZoneType { get; set; }

    public bool HasRoadAccess { get; set; }

    public bool HasElectricity { get; set; }

    public bool HasWater { get; set; }

    public bool HasSewage { get; set; }

    public bool HasWaste { get; set; }

    public float LandValue { get; set; } = 0.5f;

    public float GrowthProgress { get; set; }

    public string? BuildingId { get; set; }

    public float DistrictVitality { get; set; } = 0.5f;
}
}
