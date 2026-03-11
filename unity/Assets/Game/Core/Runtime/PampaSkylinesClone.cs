#nullable enable

namespace PampaSkylines.Core
{
using System;

public static class PampaSkylinesClone
{
    public static T DeepCopy<T>(T value)
        where T : class
    {
        if (value is null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        var json = PampaSkylinesJson.Serialize(value);
        return PampaSkylinesJson.Deserialize<T>(json)
            ?? throw new InvalidOperationException($"Unable to deep copy instance of type '{typeof(T).Name}'.");
    }
}
}
