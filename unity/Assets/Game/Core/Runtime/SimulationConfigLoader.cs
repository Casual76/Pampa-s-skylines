#nullable enable

namespace PampaSkylines.Core
{
using System;
using System.IO;

public static class SimulationConfigLoader
{
    public static SimulationConfig LoadDefault()
    {
        foreach (var candidate in GetCandidateDirectories())
        {
            if (Directory.Exists(candidate))
            {
                return LoadFromDirectory(candidate);
            }
        }

        return SimulationConfig.CreateFallback();
    }

    public static SimulationConfig LoadFromDirectory(string directoryPath)
    {
        var manifestPath = Path.Combine(directoryPath, "manifest.json");
        var manifest = PampaSkylinesJson.DeserializeCatalog<SimulationCatalogManifest>(File.ReadAllText(manifestPath))
            ?? new SimulationCatalogManifest();

        var roads = ReadJson<RoadCatalog>(directoryPath, manifest.RoadsFile);
        var services = ReadJson<ServiceCatalog>(directoryPath, manifest.ServicesFile);
        var zones = ReadJson<ZoneCatalog>(directoryPath, manifest.ZonesFile);
        var economy = ReadJson<EconomyConfig>(directoryPath, manifest.EconomyFile);
        var progression = ReadOptionalJson(directoryPath, manifest.ProgressionFile, ProgressionCatalog.CreateDefault());
        var events = ReadOptionalJson(directoryPath, manifest.EventsFile, EventCatalog.CreateDefault());

        return new SimulationConfig
        {
            Version = manifest.Version,
            Roads = roads,
            Services = services,
            Zones = zones,
            Economy = economy,
            Progression = progression,
            Events = events
        };
    }

    private static T ReadJson<T>(string directoryPath, string fileName)
        where T : class
    {
        var fullPath = Path.Combine(directoryPath, fileName);
        var json = File.ReadAllText(fullPath);
        return PampaSkylinesJson.DeserializeCatalog<T>(json) ?? throw new InvalidDataException($"Unable to parse simulation catalog '{fullPath}'.");
    }

    private static T ReadOptionalJson<T>(string directoryPath, string fileName, T fallback)
        where T : class
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return fallback;
        }

        var fullPath = Path.Combine(directoryPath, fileName);
        if (!File.Exists(fullPath))
        {
            return fallback;
        }

        var json = File.ReadAllText(fullPath);
        return PampaSkylinesJson.DeserializeCatalog<T>(json) ?? fallback;
    }

    private static string[] GetCandidateDirectories()
    {
        return new[]
        {
            Path.Combine(Environment.CurrentDirectory, "Assets", "Game", "Data", "Simulation"),
            Path.Combine(Environment.CurrentDirectory, "unity", "Assets", "Game", "Data", "Simulation"),
            Path.Combine(AppContext.BaseDirectory, "Assets", "Game", "Data", "Simulation"),
            Path.Combine(AppContext.BaseDirectory, "Simulation")
        };
    }
}
}
