namespace PampaSkylines.Core
{
public sealed class DemandState
{
    public float Residential { get; set; } = 0.55f;

    public float Commercial { get; set; } = 0.40f;

    public float Industrial { get; set; } = 0.35f;

    public float Office { get; set; } = 0.30f;
}
}
