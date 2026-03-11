#nullable enable

namespace PampaSkylines.SaveSync
{
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using PampaSkylines.Core;

public sealed class LocalCitySaveStore
{
    private readonly string _rootPath;

    public LocalCitySaveStore(string rootPath)
    {
        _rootPath = rootPath;
    }

    public string RootPath => _rootPath;

    public async Task<string> SaveAsync(CitySnapshot snapshot, CancellationToken cancellationToken = default)
    {
        var cityPath = Path.Combine(_rootPath, snapshot.CityId);
        var backupPath = Path.Combine(cityPath, "backups");
        Directory.CreateDirectory(cityPath);
        Directory.CreateDirectory(backupPath);

        var currentPath = Path.Combine(cityPath, "current.city.gz");
        var backupFile = Path.Combine(backupPath, $"{snapshot.Version}.city.gz");

        await CitySaveCodec.WriteToFileAsync(currentPath, snapshot, cancellationToken);
        await CitySaveCodec.WriteToFileAsync(backupFile, snapshot, cancellationToken);
        await WriteManifestAsync(snapshot, cancellationToken);
        return currentPath;
    }

    public async Task<CitySnapshot> LoadCurrentAsync(string cityId, CancellationToken cancellationToken = default)
    {
        var result = await LoadCurrentWithRecoveryAsync(cityId, cancellationToken);
        return result.Snapshot;
    }

    public async Task<CitySnapshot> LoadVersionAsync(string cityId, string version, CancellationToken cancellationToken = default)
    {
        var path = Path.Combine(_rootPath, cityId, "backups", $"{version}.city.gz");
        return await CitySaveCodec.ReadFromFileAsync(path, cancellationToken);
    }

    public async Task<LocalCityLoadResult> LoadCurrentWithRecoveryAsync(string cityId, CancellationToken cancellationToken = default)
    {
        var currentPath = Path.Combine(_rootPath, cityId, "current.city.gz");
        try
        {
            var snapshot = await CitySaveCodec.ReadFromFileAsync(currentPath, cancellationToken);
            return new LocalCityLoadResult
            {
                Snapshot = snapshot,
                SourceVersion = snapshot.Version
            };
        }
        catch
        {
            var manifest = await GetManifestAsync(cityId, cancellationToken);
            if (manifest is null || string.IsNullOrWhiteSpace(manifest.LastKnownGoodVersion))
            {
                throw;
            }

            var backupPath = Path.Combine(_rootPath, cityId, "backups", $"{manifest.LastKnownGoodVersion}.city.gz");
            var snapshot = await CitySaveCodec.ReadFromFileAsync(backupPath, cancellationToken);
            return new LocalCityLoadResult
            {
                Snapshot = snapshot,
                RecoveredFromBackup = true,
                SourceVersion = snapshot.Version
            };
        }
    }

    public async Task<LocalCitySaveManifest?> GetManifestAsync(string cityId, CancellationToken cancellationToken = default)
    {
        var manifestPath = Path.Combine(_rootPath, cityId, "manifest.json");
        if (!File.Exists(manifestPath))
        {
            return null;
        }

        var payload = await File.ReadAllTextAsync(manifestPath, cancellationToken);
        return PampaSkylinesJson.Deserialize<LocalCitySaveManifest>(payload);
    }

    public IReadOnlyList<string> ListBackupVersions(string cityId)
    {
        var backupPath = Path.Combine(_rootPath, cityId, "backups");
        if (!Directory.Exists(backupPath))
        {
            return new List<string>();
        }

        return Directory.EnumerateFiles(backupPath, "*.city.gz")
            .Select(static filePath =>
            {
                var fileName = Path.GetFileName(filePath);
                return fileName.EndsWith(".city.gz")
                    ? fileName[..^8]
                    : fileName;
            })
            .OrderByDescending(static name => name)
            .ToList();
    }

    public async Task<IReadOnlyList<LocalCitySlotSummary>> ListCitiesAsync(CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(_rootPath))
        {
            return new List<LocalCitySlotSummary>();
        }

        var summaries = new List<LocalCitySlotSummary>();
        foreach (var directory in Directory.EnumerateDirectories(_rootPath))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var cityId = Path.GetFileName(directory);
            if (string.IsNullOrWhiteSpace(cityId))
            {
                continue;
            }

            var manifest = await GetManifestAsync(cityId, cancellationToken);
            if (manifest is null)
            {
                continue;
            }

            summaries.Add(new LocalCitySlotSummary
            {
                CityId = manifest.CityId,
                DisplayName = string.IsNullOrWhiteSpace(manifest.DisplayName) ? manifest.CityId : manifest.DisplayName,
                CurrentVersion = manifest.CurrentVersion,
                LastKnownGoodVersion = manifest.LastKnownGoodVersion,
                LastSavedAtUtc = manifest.LastSavedAtUtc,
                LastCommandCount = manifest.LastCommandCount
            });
        }

        return summaries
            .OrderByDescending(static summary => summary.LastSavedAtUtc)
            .ThenBy(static summary => summary.DisplayName)
            .ToList();
    }

    private async Task WriteManifestAsync(CitySnapshot snapshot, CancellationToken cancellationToken)
    {
        var manifestPath = Path.Combine(_rootPath, snapshot.CityId, "manifest.json");
        var manifest = await GetManifestAsync(snapshot.CityId, cancellationToken) ?? new LocalCitySaveManifest
        {
            CityId = snapshot.CityId
        };

        manifest.DisplayName = snapshot.CityName;
        manifest.CurrentVersion = snapshot.Version;
        manifest.LastKnownGoodVersion = snapshot.Version;
        manifest.LastSavedAtUtc = snapshot.SavedAtUtc;
        manifest.LastCommandCount = snapshot.CommandCount;
        manifest.LastContentHash = snapshot.ContentHash;
        manifest.Backups.RemoveAll(entry => entry.Version == snapshot.Version);
        manifest.Backups.Add(new LocalCityBackupEntry
        {
            Version = snapshot.Version,
            SavedAtUtc = snapshot.SavedAtUtc,
            ContentHash = snapshot.ContentHash,
            CommandCount = snapshot.CommandCount
        });
        manifest.Backups = manifest.Backups
            .OrderByDescending(static entry => entry.SavedAtUtc)
            .Take(20)
            .ToList();

        var payload = PampaSkylinesJson.SerializeIndented(manifest);
        await File.WriteAllTextAsync(manifestPath, payload, cancellationToken);
    }
}
}
