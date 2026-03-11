import { describe, expect, it } from "vitest";
import { loadConfig } from "../src/config.js";
import { loadSimulationCatalogs } from "../src/game-data/catalogs.js";

describe("simulation catalogs", () => {
  it("loads and validates the shared gameplay catalogs", () => {
    const config = loadConfig();
    const catalogs = loadSimulationCatalogs(config.simulationDataRoot);

    expect(catalogs.manifest.version).toBe("v4-demo");
    expect(catalogs.roads.roadTypes.length).toBeGreaterThan(0);
    expect(catalogs.services.serviceDefinitions.length).toBeGreaterThan(0);
    expect(catalogs.zones.zoneDefinitions.length).toBeGreaterThan(0);
    expect(catalogs.progression.milestones.length).toBeGreaterThan(0);
    expect(catalogs.events.events.length).toBeGreaterThan(0);
  });
});
