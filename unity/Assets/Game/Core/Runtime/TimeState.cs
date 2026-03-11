namespace PampaSkylines.Core
{
public sealed class TimeState
{
    public bool IsPaused { get; set; }

    public float SpeedMultiplier { get; set; } = 1f;

    public float TimeOfDayHours { get; set; } = 8f;

    public int Day { get; set; } = 1;

    public void Advance(float dtHours)
    {
        if (dtHours <= 0f)
        {
            return;
        }

        TimeOfDayHours += dtHours;
        while (TimeOfDayHours >= 24f)
        {
            TimeOfDayHours -= 24f;
            Day++;
        }
    }
}
}
