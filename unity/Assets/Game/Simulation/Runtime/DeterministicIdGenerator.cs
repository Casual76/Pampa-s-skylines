namespace PampaSkylines.Simulation
{
using PampaSkylines.Core;

public static class DeterministicIdGenerator
{
    public static string Next(WorldState state, string prefix)
    {
        var nextValue = state.NextEntitySequence;
        state.NextEntitySequence++;
        return $"{prefix}-{nextValue:D8}";
    }
}
}
