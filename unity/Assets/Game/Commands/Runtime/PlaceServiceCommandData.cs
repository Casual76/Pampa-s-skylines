namespace PampaSkylines.Commands
{
using PampaSkylines.Core;
using PampaSkylines.Shared;

public sealed class PlaceServiceCommandData
{
    public ServiceType ServiceType { get; set; }

    public Int2 Cell { get; set; }

    public float CoverageRadius { get; set; } = 5f;
}
}
