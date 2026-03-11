import type { AppConfig } from "../config.js";
import type { VersionManifest } from "../contracts.js";
import { loadSimulationConfigVersion } from "../game-data/catalogs.js";

export class VersionService {
  constructor(private readonly config: AppConfig) {}

  getManifest(): VersionManifest {
    return {
      backendApiVersion: this.config.backendApiVersion,
      minimumSnapshotSchemaVersion: this.config.minimumSnapshotSchemaVersion,
      windowsBuild: this.config.windowsBuild,
      androidBuild: this.config.androidBuild,
      simulationConfigVersion: loadSimulationConfigVersion(this.config.simulationDataRoot),
      generatedAtUtc: new Date().toISOString()
    };
  }
}
