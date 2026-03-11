import path from "node:path";

export interface AppConfig {
  port: number;
  dataRoot: string;
  simulationDataRoot: string;
  jwtSecret: string;
  backendApiVersion: string;
  windowsBuild: string;
  androidBuild: string;
  minimumSnapshotSchemaVersion: number;
  firebaseProjectId?: string;
}

export function loadConfig(overrides: Partial<AppConfig> = {}): AppConfig {
  return {
    port: Number(process.env.PORT ?? 5050),
    dataRoot: path.resolve(process.cwd(), process.env.DATA_ROOT ?? "./.data"),
    simulationDataRoot: path.resolve(process.cwd(), "../unity/Assets/Game/Data/Simulation"),
    jwtSecret: process.env.APP_JWT_SECRET ?? "local-dev-secret",
    backendApiVersion: "v1",
    windowsBuild: "0.1.0-alpha",
    androidBuild: "0.1.0-alpha",
    minimumSnapshotSchemaVersion: 3,
    firebaseProjectId: process.env.FIREBASE_PROJECT_ID || undefined,
    ...overrides
  };
}
