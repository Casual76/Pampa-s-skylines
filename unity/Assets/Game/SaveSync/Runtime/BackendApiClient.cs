#nullable enable

namespace PampaSkylines.SaveSync
{
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using PampaSkylines.Core;

public sealed class BackendApiClient
{
    private readonly HttpClient _httpClient;

    public BackendApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<LoginResponse?> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsync(
            "auth/login",
            CreateJsonContent(request),
            cancellationToken);

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadAsStringAsync();
        return PampaSkylinesJson.Deserialize<LoginResponse>(payload);
    }

    public async Task<ProfileState?> GetProfileAsync(string accessToken, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "profile");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadAsStringAsync();
        return PampaSkylinesJson.Deserialize<ProfileState>(payload);
    }

    public async Task<SyncHead?> GetCityHeadAsync(string accessToken, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "city/head");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadAsStringAsync();
        return PampaSkylinesJson.Deserialize<SyncHead>(payload);
    }

    public async Task<UploadSnapshotResponse?> UploadSnapshotAsync(string accessToken, UploadSnapshotRequest requestBody, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, "city/snapshot");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Content = CreateJsonContent(requestBody);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadAsStringAsync();
        return PampaSkylinesJson.Deserialize<UploadSnapshotResponse>(payload);
    }

    public async Task<CitySnapshot?> DownloadSnapshotAsync(string accessToken, string version, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"city/snapshot/{version}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadAsStringAsync();
        return PampaSkylinesJson.Deserialize<CitySnapshot>(payload);
    }

    public async Task<VersionManifest?> GetVersionManifestAsync(CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.GetAsync("version-manifest", cancellationToken);
        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadAsStringAsync();
        return PampaSkylinesJson.Deserialize<VersionManifest>(payload);
    }

    private static StringContent CreateJsonContent<T>(T payload)
    {
        return new StringContent(PampaSkylinesJson.Serialize(payload), Encoding.UTF8, "application/json");
    }
}
}
