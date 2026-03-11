#nullable enable

namespace PampaSkylines.SaveSync
{
using Newtonsoft.Json;
using PampaSkylines.Core;

public sealed class LoginRequest
{
    [JsonProperty("username")]
    public string Username { get; set; } = string.Empty;

    [JsonProperty("password")]
    public string Password { get; set; } = string.Empty;
}

public sealed class LoginResponse
{
    [JsonProperty("accessToken")]
    public string AccessToken { get; set; } = string.Empty;

    [JsonProperty("refreshToken")]
    public string RefreshToken { get; set; } = string.Empty;

    [JsonProperty("firebaseCustomToken")]
    public string? FirebaseCustomToken { get; set; }

    [JsonProperty("profile")]
    public ProfileState Profile { get; set; } = new();
}

public sealed class RefreshRequest
{
    [JsonProperty("refreshToken")]
    public string RefreshToken { get; set; } = string.Empty;
}

public sealed class UploadSnapshotRequest
{
    [JsonProperty("head")]
    public SyncHead Head { get; set; } = new();

    [JsonProperty("snapshot")]
    public CitySnapshot Snapshot { get; set; } = new();
}

public sealed class UploadSnapshotResponse
{
    [JsonProperty("applied")]
    public bool Applied { get; set; }

    [JsonProperty("reason")]
    public string Reason { get; set; } = string.Empty;

    [JsonProperty("head")]
    public SyncHead Head { get; set; } = new();
}
}
