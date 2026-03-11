namespace PampaSkylines.Core
{
public sealed class SimulationCatalogManifest
{
    public string Version { get; set; } = "v1";

    public string RoadsFile { get; set; } = "roads.json";

    public string ServicesFile { get; set; } = "services.json";

    public string ZonesFile { get; set; } = "zones.json";

    public string EconomyFile { get; set; } = "economy.json";

    public string ProgressionFile { get; set; } = "progression.json";

    public string EventsFile { get; set; } = "events.json";
}
}
