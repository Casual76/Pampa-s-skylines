import { mkdtemp } from "node:fs/promises";
import os from "node:os";
import path from "node:path";
import { randomUUID } from "node:crypto";
import bcrypt from "bcryptjs";
import request from "supertest";
import { describe, expect, it } from "vitest";
import { createApp } from "../src/app.js";
import { loadConfig } from "../src/config.js";
import { computeSnapshotContentHash, migrateCitySnapshot } from "../src/migrations/city-snapshot.js";
import { FileStore } from "../src/storage/file-store.js";

async function setupApp() {
  const dataRoot = await mkdtemp(path.join(os.tmpdir(), "pampa-skylines-backend-"));
  const config = loadConfig({
    dataRoot,
    jwtSecret: "test-secret"
  });

  const store = new FileStore(dataRoot);
  await store.createUser({
    id: randomUUID(),
    username: "amico",
    displayName: "Amico",
    passwordHash: await bcrypt.hash("segreta", 10),
    createdAtUtc: new Date().toISOString()
  });

  return {
    app: createApp({ config, store }),
    store
  };
}

function createSnapshot(version: string, savedAtUtc: string) {
  const snapshot = {
    schemaVersion: 3,
    cityId: "city-1",
    cityName: "Citta Test",
    version,
    createdAtUtc: savedAtUtc,
    savedAtUtc,
    clientId: "pc-alpha",
    commandCount: 12,
    contentHash: "",
    metadata: {
      sourceClientId: "pc-alpha",
      sourcePlatform: "pc",
      simulationConfigVersion: "v1",
      debugLabel: "Citta Test"
    },
    state: {
      schemaVersion: 3,
      cityId: "city-1",
      cityName: "Citta Test",
      tick: 12,
      nextEntitySequence: 8,
      time: { isPaused: false, speedMultiplier: 1, timeOfDayHours: 8, day: 1 },
      budget: {
        cash: 100000,
        taxRateResidential: 0.11,
        taxRateCommercial: 0.1,
        taxRateIndustrial: 0.09,
        taxRateOffice: 0.11,
        loanBalance: 0,
        dailyServiceCost: 0,
        dailyRoadMaintenanceCost: 0,
        dailyConstructionCost: 0,
        dailyIncome: 0,
        lastDailyNet: 0
      },
      demand: { residential: 0.5, commercial: 0.4, industrial: 0.3, office: 0.2 },
      utilities: {
        electricityCoverage: 0,
        waterCoverage: 0,
        sewageCoverage: 0,
        wasteCoverage: 0,
        averageServiceCoverage: 0
      },
      roadNodes: [],
      roadSegments: [],
      lots: [],
      buildings: [],
      commuters: [],
      progression: {
        currentMilestoneIndex: 0,
        currentMilestoneId: "m0",
        currentMilestoneName: "Fondazione",
        nextMilestoneId: "m1",
        nextMilestoneName: "Borgo",
        nextMilestonePopulationTarget: 80,
        nextMilestoneRewardCash: 8000,
        reachedMilestoneIds: ["m0"],
        unlockedZones: [1],
        unlockedServices: [1, 2, 3],
        roadUnlocked: true,
        bulldozeUnlocked: true,
        budgetPolicyUnlocked: false,
        totalMilestoneRewardsAwarded: 0,
        lastMilestoneRewardCash: 0,
        lastMilestoneUnlockedId: "m0",
        lastMilestoneUnlockedName: "Fondazione",
        bailoutCount: 0,
        crisisHoursUnderThreshold: 0,
        totalSimulatedHours: 0,
        nextBailoutAvailableAtHour: 0,
        lastLoanRepaymentDay: 1
      },
      runState: {
        currentActIndex: 0,
        currentActId: "act1",
        currentActName: "Fondazione",
        currentActObjective: "Raggiungi 320 abitanti per avviare l'espansione.",
        currentActProgressValue: 0,
        currentActProgressTarget: 320,
        currentActProgress01: 0,
        activeEvent: null,
        eventHistory: [],
        eventCooldowns: [],
        activeModifiers: [],
        pendingConsequences: [],
        nextEventCheckAtHour: 4,
        fiscalDistressHours: 0,
        deficitDays: 0,
        lastDeficitTrackedDay: 1,
        isGameOver: false,
        gameOverReason: "",
        isVictory: false,
        victoryReason: "",
        victoryAtHour: 0
      },
      demoRun: {
        tutorialEnabled: true,
        onboardingStepIndex: 0,
        onboardingCompletedSteps: 0,
        onboardingCompleted: false,
        softInputLock: true,
        currentObjectiveId: "act1",
        currentObjectiveTitle: "Fondazione",
        currentObjectiveTargetPopulation: 320,
        objectivePopulation: 0,
        objectiveProgress01: 0,
        averageDistrictVitality: 0.5,
        economicPressure: 0,
        servicePressure: 0,
        trafficPressure: 0,
        runCompleted: false,
        outcome: 0,
        outcomeReason: "",
        outcomeAtHour: 0
      },
      appliedCommandCount: 12,
      averageCommuteMinutes: 0,
      averageTrafficCongestion: 0
    }
  };

  snapshot.contentHash = computeSnapshotContentHash(migrateCitySnapshot(snapshot as any));
  return snapshot;
}

function createHead(snapshot: ReturnType<typeof createSnapshot>) {
  return {
    schemaVersion: 3,
    cityId: snapshot.cityId,
    version: snapshot.version,
    displayName: snapshot.cityName,
    clientId: snapshot.clientId,
    commandCount: snapshot.commandCount,
    tick: snapshot.state.tick,
    clientUpdatedAtUtc: snapshot.savedAtUtc,
    checksum: snapshot.contentHash
  };
}

describe("backend api", () => {
  it("authenticates and returns profile", async () => {
    const { app } = await setupApp();

    const login = await request(app)
      .post("/auth/login")
      .send({ username: "amico", password: "segreta" })
      .expect(200);

    expect(login.body.accessToken).toBeTypeOf("string");
    expect(login.body.profile.username).toBe("Amico");

    const profile = await request(app)
      .get("/profile")
      .set("Authorization", `Bearer ${login.body.accessToken}`)
      .expect(200);

    expect(profile.body.username).toBe("Amico");
  });

  it("stores snapshots, accepts duplicates and rejects stale uploads", async () => {
    const { app } = await setupApp();

    const login = await request(app)
      .post("/auth/login")
      .send({ username: "amico", password: "segreta" })
      .expect(200);

    const accessToken = login.body.accessToken as string;
    const snapshot = createSnapshot("v1", "2026-03-08T08:00:00.000Z");
    const head = createHead(snapshot);

    const firstWrite = await request(app)
      .put("/city/snapshot")
      .set("Authorization", `Bearer ${accessToken}`)
      .send({ head, snapshot })
      .expect(200);

    expect(firstWrite.body.applied).toBe(true);
    expect(firstWrite.body.reason).toBe("applied");

    const duplicateWrite = await request(app)
      .put("/city/snapshot")
      .set("Authorization", `Bearer ${accessToken}`)
      .send({ head, snapshot })
      .expect(200);

    expect(duplicateWrite.body.applied).toBe(true);
    expect(duplicateWrite.body.reason).toBe("duplicate_version");

    const olderSnapshot = createSnapshot("v0", "2026-03-08T07:59:00.000Z");
    const olderWrite = await request(app)
      .put("/city/snapshot")
      .set("Authorization", `Bearer ${accessToken}`)
      .send({
        head: createHead(olderSnapshot),
        snapshot: olderSnapshot
      })
      .expect(200);

    expect(olderWrite.body.applied).toBe(false);
    expect(olderWrite.body.reason).toBe("stale_head");

    const headResponse = await request(app)
      .get("/city/head")
      .set("Authorization", `Bearer ${accessToken}`)
      .expect(200);

    expect(headResponse.body.version).toBe("v1");

    const downloaded = await request(app)
      .get("/city/snapshot/v1")
      .set("Authorization", `Bearer ${accessToken}`)
      .expect(200);

    expect(downloaded.body.cityName).toBe("Citta Test");
    expect(downloaded.body.contentHash).toBe(snapshot.contentHash);
  });

  it("rejects checksum mismatches", async () => {
    const { app } = await setupApp();

    const login = await request(app)
      .post("/auth/login")
      .send({ username: "amico", password: "segreta" })
      .expect(200);

    const accessToken = login.body.accessToken as string;
    const snapshot = createSnapshot("v1", "2026-03-08T08:00:00.000Z");
    const head = {
      ...createHead(snapshot),
      checksum: "not-the-real-hash"
    };

    const response = await request(app)
      .put("/city/snapshot")
      .set("Authorization", `Bearer ${accessToken}`)
      .send({ head, snapshot })
      .expect(200);

    expect(response.body.applied).toBe(false);
    expect(response.body.reason).toBe("checksum_mismatch");
  });

  it("migrates legacy snapshots missing the new envelope fields", async () => {
    const { app } = await setupApp();

    const login = await request(app)
      .post("/auth/login")
      .send({ username: "amico", password: "segreta" })
      .expect(200);

    const accessToken = login.body.accessToken as string;
    const legacySnapshot = {
      schemaVersion: 1,
      cityId: "city-legacy",
      cityName: "Legacy City",
      version: "legacy-v1",
      savedAtUtc: "2026-03-08T08:00:00.000Z",
      state: {
        schemaVersion: 1,
        cityId: "city-legacy",
        cityName: "Legacy City",
        tick: 3,
        nextEntitySequence: 1,
        time: { isPaused: false, speedMultiplier: 1, timeOfDayHours: 8, day: 1 },
        budget: {
          cash: 100000,
          taxRateResidential: 0.11,
          taxRateCommercial: 0.1,
          taxRateIndustrial: 0.09,
          taxRateOffice: 0.11,
          loanBalance: 0,
          dailyServiceCost: 0,
          dailyRoadMaintenanceCost: 0,
          dailyConstructionCost: 0,
          dailyIncome: 0,
          lastDailyNet: 0
        },
        demand: { residential: 0.5, commercial: 0.4, industrial: 0.3, office: 0.2 },
        utilities: {
          electricityCoverage: 0,
          waterCoverage: 0,
          sewageCoverage: 0,
          wasteCoverage: 0,
          averageServiceCoverage: 0
        },
        roadNodes: [],
        roadSegments: [],
        lots: [],
        buildings: [],
        commuters: [],
        appliedCommandCount: 3,
        averageCommuteMinutes: 0,
        averageTrafficCongestion: 0
      }
    };

    const migratedSnapshot = {
      ...legacySnapshot,
      schemaVersion: 4,
      createdAtUtc: legacySnapshot.savedAtUtc,
      clientId: "local",
      commandCount: 3,
      metadata: {
        sourceClientId: "local",
        sourcePlatform: "unknown",
        simulationConfigVersion: "unknown",
        debugLabel: "Legacy City",
        saveSlotId: "autosave",
        saveReason: "manual",
        lastSyncStatus: "pending"
      },
      state: {
        ...legacySnapshot.state,
        schemaVersion: 4,
        progression: {
          currentMilestoneIndex: 0,
          currentMilestoneId: "m0",
          currentMilestoneName: "Fondazione",
          nextMilestoneId: "m1",
          nextMilestoneName: "Borgo",
          nextMilestonePopulationTarget: 80,
          nextMilestoneRewardCash: 8000,
          reachedMilestoneIds: ["m0"],
          unlockedZones: [1],
          unlockedServices: [1, 2, 3],
          roadUnlocked: true,
          bulldozeUnlocked: true,
          budgetPolicyUnlocked: false,
          totalMilestoneRewardsAwarded: 0,
          lastMilestoneRewardCash: 0,
          lastMilestoneUnlockedId: "m0",
          lastMilestoneUnlockedName: "Fondazione",
          bailoutCount: 0,
          crisisHoursUnderThreshold: 0,
          totalSimulatedHours: 0,
          nextBailoutAvailableAtHour: 0,
          lastLoanRepaymentDay: 1
        },
        runState: {
          currentActIndex: 0,
          currentActId: "act1",
          currentActName: "Fondazione",
          currentActObjective: "Raggiungi 320 abitanti per avviare l'espansione.",
          currentActProgressValue: 0,
          currentActProgressTarget: 320,
          currentActProgress01: 0,
          activeEvent: null,
          eventHistory: [],
          eventCooldowns: [],
          activeModifiers: [],
          pendingConsequences: [],
          nextEventCheckAtHour: 4,
          fiscalDistressHours: 0,
          deficitDays: 0,
          lastDeficitTrackedDay: 1,
          isGameOver: false,
          gameOverReason: "",
          isVictory: false,
          victoryReason: "",
          victoryAtHour: 0
        },
        demoRun: {
          tutorialEnabled: true,
          onboardingStepIndex: 0,
          onboardingCompletedSteps: 0,
          onboardingCompleted: false,
          softInputLock: true,
          currentObjectiveId: "act1",
          currentObjectiveTitle: "Fondazione",
          currentObjectiveTargetPopulation: 320,
          objectivePopulation: 0,
          objectiveProgress01: 0,
          averageDistrictVitality: 0.5,
          economicPressure: 0,
          servicePressure: 0,
          trafficPressure: 0,
          runCompleted: false,
          outcome: 0,
          outcomeReason: "",
          outcomeAtHour: 0
        }
      },
      contentHash: ""
    };
    migratedSnapshot.contentHash = computeSnapshotContentHash(migrateCitySnapshot(legacySnapshot as any));

    const response = await request(app)
      .put("/city/snapshot")
      .set("Authorization", `Bearer ${accessToken}`)
      .send({
        head: {
          schemaVersion: 3,
          cityId: migratedSnapshot.cityId,
          version: migratedSnapshot.version,
          displayName: migratedSnapshot.cityName,
          clientId: "local",
          commandCount: 3,
          tick: 3,
          clientUpdatedAtUtc: migratedSnapshot.savedAtUtc,
          checksum: migratedSnapshot.contentHash
        },
        snapshot: legacySnapshot
      })
      .expect(200);

    expect(response.body.applied).toBe(true);
    expect(response.body.reason).toBe("applied");
  });

  it("returns the current version manifest", async () => {
    const { app } = await setupApp();
    const response = await request(app).get("/version-manifest").expect(200);
    expect(response.body.backendApiVersion).toBe("v1");
    expect(response.body.windowsBuild).toBe("0.1.0-alpha");
    expect(response.body.simulationConfigVersion).toBe("v4-demo");
  });
});
