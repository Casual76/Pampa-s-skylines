import { createHash } from "node:crypto";
import type { CitySnapshot, RawCitySnapshot } from "../contracts.js";

interface ProgressionMilestone {
  id: string;
  name: string;
  population: number;
  reward: number;
  unlockZones: number[];
  unlockServices: number[];
  unlockRoad?: boolean;
  unlockBulldoze?: boolean;
  unlockBudgetPolicy?: boolean;
}

interface RunActDefinition {
  id: string;
  name: string;
  minPopulation: number;
  maxPopulation: number;
  targetPopulation: number;
  objective: string;
}

const progressionMilestones: ProgressionMilestone[] = [
  {
    id: "m0",
    name: "Fondazione",
    population: 0,
    reward: 0,
    unlockRoad: true,
    unlockBulldoze: true,
    unlockZones: [1],
    unlockServices: [1, 2, 3]
  },
  {
    id: "m1",
    name: "Borgo",
    population: 80,
    reward: 8000,
    unlockZones: [2],
    unlockServices: [4]
  },
  {
    id: "m2",
    name: "Distretto Lavoro",
    population: 180,
    reward: 12000,
    unlockZones: [3],
    unlockServices: []
  },
  {
    id: "m3",
    name: "Municipio",
    population: 320,
    reward: 15000,
    unlockBudgetPolicy: true,
    unlockZones: [4],
    unlockServices: []
  },
  {
    id: "m4",
    name: "Sicurezza",
    population: 500,
    reward: 20000,
    unlockZones: [],
    unlockServices: [5]
  },
  {
    id: "m5",
    name: "Ordine Civico",
    population: 700,
    reward: 25000,
    unlockZones: [],
    unlockServices: [6]
  },
  {
    id: "m6",
    name: "Sanita Pubblica",
    population: 950,
    reward: 30000,
    unlockZones: [],
    unlockServices: [7]
  },
  {
    id: "m7",
    name: "Citta della Conoscenza",
    population: 1250,
    reward: 40000,
    unlockZones: [],
    unlockServices: [8]
  }
];

const runActs: RunActDefinition[] = [
  {
    id: "act1",
    name: "Fondazione",
    minPopulation: 0,
    maxPopulation: 319,
    targetPopulation: 320,
    objective: "Raggiungi 320 abitanti per avviare l'espansione."
  },
  {
    id: "act2",
    name: "Espansione",
    minPopulation: 320,
    maxPopulation: 949,
    targetPopulation: 950,
    objective: "Consolida servizi e lavoro fino a 950 abitanti."
  },
  {
    id: "act3",
    name: "Pressione Metropolitana",
    minPopulation: 950,
    maxPopulation: Number.MAX_SAFE_INTEGER,
    targetPopulation: 1250,
    objective: "Mantieni la citta stabile durante la crescita metropolitana."
  }
];

export function migrateCitySnapshot(snapshot: RawCitySnapshot): CitySnapshot {
  const createdAtUtc = snapshot.createdAtUtc ?? snapshot.savedAtUtc ?? new Date().toISOString();
  const savedAtUtc = snapshot.savedAtUtc ?? createdAtUtc;
  const clientId = snapshot.clientId ?? "local";
  const commandCount = Math.max(snapshot.commandCount ?? 0, snapshot.state.appliedCommandCount ?? 0);

  const migrated: CitySnapshot = {
    schemaVersion: 4,
    cityId: snapshot.cityId,
    cityName: snapshot.cityName,
    version: snapshot.version,
    createdAtUtc,
    savedAtUtc,
    clientId,
    commandCount,
    contentHash: snapshot.contentHash ?? "",
    metadata: {
      sourceClientId: snapshot.metadata?.sourceClientId ?? clientId,
      sourcePlatform: snapshot.metadata?.sourcePlatform ?? "unknown",
      simulationConfigVersion: snapshot.metadata?.simulationConfigVersion ?? "unknown",
      debugLabel: snapshot.metadata?.debugLabel ?? snapshot.cityName,
      saveSlotId: snapshot.metadata?.saveSlotId ?? "autosave",
      saveReason: snapshot.metadata?.saveReason ?? "manual",
      lastSyncStatus: snapshot.metadata?.lastSyncStatus ?? "pending"
    },
    state: {
      ...snapshot.state,
      schemaVersion: 4,
      progression: migrateProgressionState(snapshot.state),
      runState: migrateRunState(snapshot.state),
      demoRun: migrateDemoRunState(snapshot.state)
    }
  };

  if (!migrated.contentHash) {
    migrated.contentHash = computeSnapshotContentHash(migrated);
  }

  return migrated;
}

export function computeSnapshotContentHash(snapshot: CitySnapshot): string {
  const normalized = {
    ...snapshot,
    contentHash: ""
  };

  return createHash("sha256")
    .update(stableStringify(normalized, true))
    .digest("hex");
}

function migrateProgressionState(state: RawCitySnapshot["state"]) {
  const population = computePopulation(state);
  const fallback = createProgressionFromPopulation(population, state.time?.day ?? 1);
  const input = state.progression;
  if (!input) {
    return fallback;
  }

  const unlockedZones = new Set<number>([...fallback.unlockedZones, ...(input.unlockedZones ?? [])]);
  const unlockedServices = new Set<number>([...fallback.unlockedServices, ...(input.unlockedServices ?? [])]);
  const reachedMilestoneIds = new Set<string>([...fallback.reachedMilestoneIds, ...(input.reachedMilestoneIds ?? [])]);

  return {
    currentMilestoneIndex: Math.max(fallback.currentMilestoneIndex, input.currentMilestoneIndex ?? fallback.currentMilestoneIndex),
    currentMilestoneId: input.currentMilestoneId ?? fallback.currentMilestoneId,
    currentMilestoneName: input.currentMilestoneName ?? fallback.currentMilestoneName,
    nextMilestoneId: input.nextMilestoneId ?? fallback.nextMilestoneId,
    nextMilestoneName: input.nextMilestoneName ?? fallback.nextMilestoneName,
    nextMilestonePopulationTarget: input.nextMilestonePopulationTarget ?? fallback.nextMilestonePopulationTarget,
    nextMilestoneRewardCash: input.nextMilestoneRewardCash ?? fallback.nextMilestoneRewardCash,
    reachedMilestoneIds: [...reachedMilestoneIds],
    unlockedZones: [...unlockedZones],
    unlockedServices: [...unlockedServices],
    roadUnlocked: input.roadUnlocked ?? fallback.roadUnlocked,
    bulldozeUnlocked: input.bulldozeUnlocked ?? fallback.bulldozeUnlocked,
    budgetPolicyUnlocked: input.budgetPolicyUnlocked ?? fallback.budgetPolicyUnlocked,
    totalMilestoneRewardsAwarded: Math.max(input.totalMilestoneRewardsAwarded ?? 0, fallback.totalMilestoneRewardsAwarded),
    lastMilestoneRewardCash: input.lastMilestoneRewardCash ?? fallback.lastMilestoneRewardCash,
    lastMilestoneUnlockedId: input.lastMilestoneUnlockedId ?? fallback.lastMilestoneUnlockedId,
    lastMilestoneUnlockedName: input.lastMilestoneUnlockedName ?? fallback.lastMilestoneUnlockedName,
    bailoutCount: input.bailoutCount ?? 0,
    crisisHoursUnderThreshold: input.crisisHoursUnderThreshold ?? 0,
    totalSimulatedHours: input.totalSimulatedHours ?? 0,
    nextBailoutAvailableAtHour: input.nextBailoutAvailableAtHour ?? 0,
    lastLoanRepaymentDay: Math.max(1, input.lastLoanRepaymentDay ?? state.time?.day ?? 1)
  };
}

function createProgressionFromPopulation(population: number, currentDay: number) {
  const milestones = progressionMilestones.filter((milestone) => population >= milestone.population);
  const currentMilestone = milestones[milestones.length - 1] ?? progressionMilestones[0];
  const nextMilestone = progressionMilestones[Math.min(currentMilestoneIndex(population) + 1, progressionMilestones.length - 1)] ?? currentMilestone;

  const unlockedZones = new Set<number>();
  const unlockedServices = new Set<number>();
  let roadUnlocked = false;
  let bulldozeUnlocked = false;
  let budgetPolicyUnlocked = false;
  let totalRewards = 0;

  for (const milestone of milestones) {
    for (const zone of milestone.unlockZones) {
      unlockedZones.add(zone);
    }

    for (const service of milestone.unlockServices) {
      unlockedServices.add(service);
    }

    roadUnlocked = roadUnlocked || Boolean(milestone.unlockRoad);
    bulldozeUnlocked = bulldozeUnlocked || Boolean(milestone.unlockBulldoze);
    budgetPolicyUnlocked = budgetPolicyUnlocked || Boolean(milestone.unlockBudgetPolicy);
    totalRewards += milestone.reward;
  }

  return {
    currentMilestoneIndex: currentMilestoneIndex(population),
    currentMilestoneId: currentMilestone.id,
    currentMilestoneName: currentMilestone.name,
    nextMilestoneId: nextMilestone.id,
    nextMilestoneName: nextMilestone.name,
    nextMilestonePopulationTarget: nextMilestone.population,
    nextMilestoneRewardCash: nextMilestone.reward,
    reachedMilestoneIds: milestones.map((milestone) => milestone.id),
    unlockedZones: [...unlockedZones],
    unlockedServices: [...unlockedServices],
    roadUnlocked,
    bulldozeUnlocked,
    budgetPolicyUnlocked,
    totalMilestoneRewardsAwarded: totalRewards,
    lastMilestoneRewardCash: 0,
    lastMilestoneUnlockedId: currentMilestone.id,
    lastMilestoneUnlockedName: currentMilestone.name,
    bailoutCount: 0,
    crisisHoursUnderThreshold: 0,
    totalSimulatedHours: 0,
    nextBailoutAvailableAtHour: 0,
    lastLoanRepaymentDay: Math.max(1, currentDay)
  };
}

function migrateRunState(state: RawCitySnapshot["state"]) {
  const population = computePopulation(state);
  const fallback = createRunStateFromPopulation(population, state.time?.day ?? 1);
  const input = state.runState;
  if (!input) {
    return fallback;
  }

  const normalizedHistory = (input.eventHistory ?? [])
    .filter((entry) => typeof entry.eventId === "string" && entry.eventId.length > 0)
    .slice(0, 24);

  const cooldowns = dedupeCooldowns(input.eventCooldowns ?? []);
  const modifiers = (input.activeModifiers ?? [])
    .filter((modifier) => typeof modifier.modifierId === "string" && modifier.modifierId.length > 0);
  const pendingConsequences = migratePendingConsequences(input.pendingConsequences ?? []);

  const activeEvent = input.activeEvent && input.activeEvent.eventId
    ? {
        eventId: input.activeEvent.eventId,
        title: input.activeEvent.title ?? input.activeEvent.eventId,
        description: input.activeEvent.description ?? "",
        triggeredAtHour: input.activeEvent.triggeredAtHour ?? 0,
        choices: (input.activeEvent.choices ?? []).map((choice) => ({
          choiceId: choice.choiceId,
          label: choice.label,
          description: choice.description
        }))
      }
    : null;

  const resolvedAct = resolveAct(population);
  const currentActIndex = Math.max(resolvedAct.index, input.currentActIndex ?? fallback.currentActIndex);
  const resolvedDefinition = runActs[Math.min(currentActIndex, runActs.length - 1)] ?? runActs[0];

  const progressTarget = Math.max(1, input.currentActProgressTarget ?? resolvedDefinition.targetPopulation);
  const progressValue = Math.max(population, input.currentActProgressValue ?? population);

  return {
    currentActIndex,
    currentActId: input.currentActId ?? resolvedDefinition.id,
    currentActName: input.currentActName ?? resolvedDefinition.name,
    currentActObjective: input.currentActObjective ?? resolvedDefinition.objective,
    currentActProgressValue: progressValue,
    currentActProgressTarget: progressTarget,
    currentActProgress01: Math.max(0, Math.min(1, input.currentActProgress01 ?? (progressValue / progressTarget))),
    activeEvent,
    eventHistory: normalizedHistory,
    eventCooldowns: cooldowns,
    activeModifiers: modifiers,
    pendingConsequences,
    nextEventCheckAtHour: Math.max(0, input.nextEventCheckAtHour ?? fallback.nextEventCheckAtHour),
    fiscalDistressHours: Math.max(0, input.fiscalDistressHours ?? 0),
    deficitDays: Math.max(0, input.deficitDays ?? 0),
    lastDeficitTrackedDay: Math.max(1, input.lastDeficitTrackedDay ?? fallback.lastDeficitTrackedDay),
    isGameOver: Boolean(input.isGameOver),
    gameOverReason: input.gameOverReason ?? "",
    isVictory: Boolean(input.isVictory),
    victoryReason: input.victoryReason ?? "",
    victoryAtHour: Math.max(0, input.victoryAtHour ?? 0)
  };
}

function createRunStateFromPopulation(population: number, currentDay: number) {
  const resolvedAct = resolveAct(population);
  return {
    currentActIndex: resolvedAct.index,
    currentActId: resolvedAct.act.id,
    currentActName: resolvedAct.act.name,
    currentActObjective: resolvedAct.act.objective,
    currentActProgressValue: population,
    currentActProgressTarget: resolvedAct.act.targetPopulation,
    currentActProgress01: Math.max(0, Math.min(1, population / Math.max(1, resolvedAct.act.targetPopulation))),
    activeEvent: null,
    eventHistory: [],
    eventCooldowns: [],
    activeModifiers: [],
    pendingConsequences: [],
    nextEventCheckAtHour: 4,
    fiscalDistressHours: 0,
    deficitDays: 0,
    lastDeficitTrackedDay: Math.max(1, currentDay),
    isGameOver: false,
    gameOverReason: "",
    isVictory: false,
    victoryReason: "",
    victoryAtHour: 0
  };
}

function migratePendingConsequences(pendingConsequences: Array<{
  sourceEventId?: string;
  consequenceId?: string;
  label?: string;
  applyAtHour?: number;
  followUpEventId?: string;
  cashDelta?: number;
  loanDelta?: number;
  residentialDemandDelta?: number;
  commercialDemandDelta?: number;
  industrialDemandDelta?: number;
  officeDemandDelta?: number;
  landValueDelta?: number;
  utilityCoverageDelta?: number;
  timedModifiers?: Array<{
    id?: string;
    modifierId?: string;
    label?: string;
    durationHours?: number;
    expiresAtHour?: number;
    residentialDemandMultiplier?: number;
    commercialDemandMultiplier?: number;
    industrialDemandMultiplier?: number;
    officeDemandMultiplier?: number;
    growthMultiplier?: number;
    serviceCostMultiplier?: number;
    roadMaintenanceMultiplier?: number;
    commuteMinutesMultiplier?: number;
    taxIncomeMultiplier?: number;
  }>;
}>) {
  return pendingConsequences
    .filter((consequence) => typeof consequence.consequenceId === "string" && consequence.consequenceId.length > 0)
    .map((consequence) => ({
      sourceEventId: consequence.sourceEventId ?? "unknown",
      consequenceId: consequence.consequenceId ?? "unknown",
      label: consequence.label ?? consequence.consequenceId ?? "conseguenza",
      applyAtHour: Math.max(0, consequence.applyAtHour ?? 0),
      followUpEventId: consequence.followUpEventId ?? "",
      cashDelta: consequence.cashDelta ?? 0,
      loanDelta: consequence.loanDelta ?? 0,
      residentialDemandDelta: consequence.residentialDemandDelta ?? 0,
      commercialDemandDelta: consequence.commercialDemandDelta ?? 0,
      industrialDemandDelta: consequence.industrialDemandDelta ?? 0,
      officeDemandDelta: consequence.officeDemandDelta ?? 0,
      landValueDelta: consequence.landValueDelta ?? 0,
      utilityCoverageDelta: consequence.utilityCoverageDelta ?? 0,
      timedModifiers: (consequence.timedModifiers ?? [])
        .filter((modifier) =>
          (typeof modifier.id === "string" && modifier.id.length > 0) ||
          (typeof modifier.modifierId === "string" && modifier.modifierId.length > 0))
        .map((modifier) => ({
          id: modifier.id ?? modifier.modifierId ?? "modifier",
          label: modifier.label ?? modifier.id ?? modifier.modifierId ?? "Modificatore",
          durationHours: Math.max(0.25, modifier.durationHours ?? modifier.expiresAtHour ?? 8),
          residentialDemandMultiplier: Math.max(0.05, modifier.residentialDemandMultiplier ?? 1),
          commercialDemandMultiplier: Math.max(0.05, modifier.commercialDemandMultiplier ?? 1),
          industrialDemandMultiplier: Math.max(0.05, modifier.industrialDemandMultiplier ?? 1),
          officeDemandMultiplier: Math.max(0.05, modifier.officeDemandMultiplier ?? 1),
          growthMultiplier: Math.max(0.05, modifier.growthMultiplier ?? 1),
          serviceCostMultiplier: Math.max(0.05, modifier.serviceCostMultiplier ?? 1),
          roadMaintenanceMultiplier: Math.max(0.05, modifier.roadMaintenanceMultiplier ?? 1),
          commuteMinutesMultiplier: Math.max(0.05, modifier.commuteMinutesMultiplier ?? 1),
          taxIncomeMultiplier: Math.max(0.05, modifier.taxIncomeMultiplier ?? 1)
        }))
    }));
}

function migrateDemoRunState(state: RawCitySnapshot["state"]) {
  const population = computePopulation(state);
  const runState = migrateRunState(state);
  const input = state.demoRun;
  const objectiveTarget = Math.max(1, runState.currentActProgressTarget ?? 320);
  const objectiveProgress = Math.max(0, Math.min(1, runState.currentActProgress01 ?? (population / objectiveTarget)));
  const onboardingCompletedSteps = Math.max(0, input?.onboardingCompletedSteps ?? 0);
  const onboardingCompleted = Boolean(input?.onboardingCompleted);
  const onboardingStepIndex = Math.max(0, input?.onboardingStepIndex ?? onboardingCompletedSteps);
  const tutorialEnabled = input?.tutorialEnabled ?? true;

  let onboardingStepId = input?.onboardingStepId ?? "onb-01-road";
  let onboardingStepTitle = input?.onboardingStepTitle ?? "Apri la rete viaria";
  let onboardingStepInstruction =
    input?.onboardingStepInstruction ?? "Traccia la prima strada per connettere i lotti iniziali.";
  let onboardingFocusTool = input?.onboardingFocusTool ?? "Strada";

  if (!tutorialEnabled) {
    onboardingStepId = "onb-complete";
    onboardingStepTitle = "Onboarding disattivato";
    onboardingStepInstruction = "Tutorial disattivato: guida contestuale e lock morbidi non attivi.";
    onboardingFocusTool = "Nessuno";
  } else if (onboardingCompleted) {
    onboardingStepId = "onb-complete";
    onboardingStepTitle = "Onboarding completato";
    onboardingStepInstruction = "Tutti gli strumenti demo sono ora disponibili.";
    onboardingFocusTool = "Gestione libera";
  }

  return {
    onboardingStepId,
    onboardingStepTitle,
    onboardingStepInstruction,
    onboardingFocusTool,
    tutorialEnabled,
    onboardingStepIndex,
    onboardingCompletedSteps,
    onboardingCompleted,
    softInputLock: input?.softInputLock ?? true,
    currentObjectiveId: input?.currentObjectiveId ?? runState.currentActId ?? "act1",
    currentObjectiveTitle: input?.currentObjectiveTitle ?? runState.currentActName ?? "Fondazione",
    currentObjectiveTargetPopulation: Math.max(1, input?.currentObjectiveTargetPopulation ?? objectiveTarget),
    objectivePopulation: Math.max(0, input?.objectivePopulation ?? population),
    objectiveProgress01: Math.max(0, Math.min(1, input?.objectiveProgress01 ?? objectiveProgress)),
    averageDistrictVitality: Math.max(0, Math.min(1, input?.averageDistrictVitality ?? 0.5)),
    economicPressure: Math.max(0, Math.min(1, input?.economicPressure ?? 0)),
    servicePressure: Math.max(0, Math.min(1, input?.servicePressure ?? 0)),
    trafficPressure: Math.max(0, Math.min(1, input?.trafficPressure ?? 0)),
    runCompleted: Boolean(input?.runCompleted ?? (runState.isGameOver || runState.isVictory)),
    outcome: Math.max(0, input?.outcome ?? (runState.isGameOver ? 2 : runState.isVictory ? 1 : 0)),
    outcomeReason: input?.outcomeReason ?? runState.gameOverReason ?? runState.victoryReason ?? "",
    outcomeAtHour: Math.max(0, input?.outcomeAtHour ?? runState.victoryAtHour ?? 0)
  };
}

function resolveAct(population: number): { index: number; act: RunActDefinition } {
  let index = 0;
  for (let candidate = 0; candidate < runActs.length; candidate++) {
    const act = runActs[candidate];
    if (population >= act.minPopulation && population <= act.maxPopulation) {
      index = candidate;
    }
  }

  return {
    index,
    act: runActs[index]
  };
}

function dedupeCooldowns(cooldowns: Array<{ eventId: string; availableAtHour: number }>) {
  const map = new Map<string, number>();
  for (const cooldown of cooldowns) {
    if (!cooldown.eventId) {
      continue;
    }

    const previous = map.get(cooldown.eventId);
    const candidate = Number.isFinite(cooldown.availableAtHour) ? cooldown.availableAtHour : 0;
    if (previous === undefined || candidate > previous) {
      map.set(cooldown.eventId, candidate);
    }
  }

  return [...map.entries()]
    .map(([eventId, availableAtHour]) => ({ eventId, availableAtHour }))
    .sort((left, right) => left.eventId.localeCompare(right.eventId, "en"));
}

function currentMilestoneIndex(population: number): number {
  let currentIndex = 0;
  for (let index = 0; index < progressionMilestones.length; index++) {
    if (population >= progressionMilestones[index].population) {
      currentIndex = index;
    }
  }

  return currentIndex;
}

function computePopulation(state: RawCitySnapshot["state"]): number {
  return (state.buildings ?? []).reduce((total, building) => total + (building.residents ?? 0), 0);
}

function stableStringify(value: unknown, sortArrays: boolean): string {
  return JSON.stringify(canonicalize(value, sortArrays));
}

function canonicalize(value: unknown, sortArrays: boolean): unknown {
  if (Array.isArray(value)) {
    const mapped = value.map((item) => canonicalize(item, sortArrays));
    if (!sortArrays) {
      return mapped;
    }

    return mapped.sort((left, right) =>
      stableStringify(left, sortArrays).localeCompare(stableStringify(right, sortArrays), "en"));
  }

  if (value && typeof value === "object") {
    return Object.entries(value as Record<string, unknown>)
      .sort(([left], [right]) => left.localeCompare(right, "en"))
      .reduce<Record<string, unknown>>((accumulator, [key, nestedValue]) => {
        accumulator[key] = canonicalize(nestedValue, sortArrays);
        return accumulator;
      }, {});
  }

  return value;
}
