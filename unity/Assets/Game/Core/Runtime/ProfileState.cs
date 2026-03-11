#nullable enable

namespace PampaSkylines.Core
{
public sealed class ProfileState
{
    public string UserId { get; set; } = string.Empty;

    public string Username { get; set; } = string.Empty;

    public SyncHead? ActiveCityHead { get; set; }
}
}
