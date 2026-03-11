namespace PampaSkylines.Core
{
public sealed class RoadSegment
{
    public string Id { get; set; } = string.Empty;

    public string RoadTypeId { get; set; } = "road-2lane";

    public string FromNodeId { get; set; } = string.Empty;

    public string ToNodeId { get; set; } = string.Empty;

    public int Lanes { get; set; } = 2;

    public int Capacity { get; set; } = 1200;

    public double Length { get; set; }

    public float Congestion { get; set; }
}
}
