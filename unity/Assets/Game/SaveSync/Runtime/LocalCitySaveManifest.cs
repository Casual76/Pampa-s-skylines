#nullable enable

namespace PampaSkylines.SaveSync
{
using System;
using System.Collections.Generic;
using PampaSkylines.Core;

public sealed class LocalCitySaveManifest
{
    public int SchemaVersion { get; set; } = 1;

    public string CityId { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string CurrentVersion { get; set; } = string.Empty;

    public string LastKnownGoodVersion { get; set; } = string.Empty;

    public DateTimeOffset LastSavedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public long LastCommandCount { get; set; }

    public string LastContentHash { get; set; } = string.Empty;

    public List<LocalCityBackupEntry> Backups { get; set; } = new();
}

public sealed class LocalCityBackupEntry
{
    public string Version { get; set; } = string.Empty;

    public DateTimeOffset SavedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public string ContentHash { get; set; } = string.Empty;

    public long CommandCount { get; set; }
}

public sealed class LocalCityLoadResult
{
    public CitySnapshot Snapshot { get; set; } = new();

    public bool RecoveredFromBackup { get; set; }

    public string SourceVersion { get; set; } = string.Empty;
}

public sealed class LocalCitySlotSummary
{
    public string CityId { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string CurrentVersion { get; set; } = string.Empty;

    public string LastKnownGoodVersion { get; set; } = string.Empty;

    public DateTimeOffset LastSavedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public long LastCommandCount { get; set; }
}
}
