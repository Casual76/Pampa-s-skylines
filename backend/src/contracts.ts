import { z } from "zod";

const int2Schema = z.object({
  x: z.number().int(),
  y: z.number().int()
});

const timeStateSchema = z.object({
  isPaused: z.boolean(),
  speedMultiplier: z.number(),
  timeOfDayHours: z.number(),
  day: z.number().int()
});

const budgetStateSchema = z.object({
  cash: z.number(),
  taxRateResidential: z.number(),
  taxRateCommercial: z.number(),
  taxRateIndustrial: z.number(),
  taxRateOffice: z.number(),
  loanBalance: z.number(),
  dailyServiceCost: z.number(),
  dailyRoadMaintenanceCost: z.number(),
  dailyConstructionCost: z.number(),
  dailyIncome: z.number(),
  lastDailyNet: z.number()
});

const demandStateSchema = z.object({
  residential: z.number(),
  commercial: z.number(),
  industrial: z.number(),
  office: z.number()
});

const utilityStateSchema = z.object({
  electricityCoverage: z.number(),
  waterCoverage: z.number(),
  sewageCoverage: z.number(),
  wasteCoverage: z.number(),
  averageServiceCoverage: z.number()
});

const roadNodeSchema = z.object({
  id: z.string().min(1),
  position: int2Schema
});

const roadSegmentSchema = z.object({
  id: z.string().min(1),
  roadTypeId: z.string().min(1),
  fromNodeId: z.string().min(1),
  toNodeId: z.string().min(1),
  lanes: z.number().int(),
  capacity: z.number().int(),
  length: z.number(),
  congestion: z.number()
});

const zoneLotSchema = z.object({
  id: z.string().min(1),
  cell: int2Schema,
  zoneType: z.number().int(),
  hasRoadAccess: z.boolean(),
  hasElectricity: z.boolean(),
  hasWater: z.boolean(),
  hasSewage: z.boolean(),
  hasWaste: z.boolean(),
  landValue: z.number(),
  growthProgress: z.number(),
  buildingId: z.string().nullable().optional(),
  districtVitality: z.number().optional().default(0.5)
});

const buildingStateSchema = z.object({
  id: z.string().min(1),
  lotId: z.string().min(1),
  cell: int2Schema,
  zoneType: z.number().int(),
  serviceType: z.number().int(),
  level: z.number().int(),
  residents: z.number().int(),
  jobs: z.number().int(),
  condition: z.number(),
  coverageRadius: z.number(),
  districtVitality: z.number().optional().default(0.5)
});

const commuterAgentSchema = z.object({
  id: z.string().min(1),
  homeBuildingId: z.string().min(1),
  workBuildingId: z.string().min(1),
  currentRoadSegmentId: z.string().nullable().optional(),
  commuteMinutes: z.number()
});

const progressionStateSchema = z.object({
  currentMilestoneIndex: z.number().int().nonnegative(),
  currentMilestoneId: z.string().min(1),
  currentMilestoneName: z.string().min(1),
  nextMilestoneId: z.string(),
  nextMilestoneName: z.string(),
  nextMilestonePopulationTarget: z.number().int().nonnegative(),
  nextMilestoneRewardCash: z.number(),
  reachedMilestoneIds: z.array(z.string().min(1)),
  unlockedZones: z.array(z.number().int()),
  unlockedServices: z.array(z.number().int()),
  roadUnlocked: z.boolean(),
  bulldozeUnlocked: z.boolean(),
  budgetPolicyUnlocked: z.boolean(),
  totalMilestoneRewardsAwarded: z.number(),
  lastMilestoneRewardCash: z.number(),
  lastMilestoneUnlockedId: z.string(),
  lastMilestoneUnlockedName: z.string(),
  bailoutCount: z.number().int().nonnegative(),
  crisisHoursUnderThreshold: z.number(),
  totalSimulatedHours: z.number(),
  nextBailoutAvailableAtHour: z.number(),
  lastLoanRepaymentDay: z.number().int().nonnegative()
});

const activeCityEventChoiceSchema = z.object({
  choiceId: z.string().min(1),
  label: z.string().min(1),
  description: z.string().min(1)
});

const activeCityEventSchema = z.object({
  eventId: z.string().min(1),
  title: z.string().min(1),
  description: z.string().min(1),
  triggeredAtHour: z.number(),
  choices: z.array(activeCityEventChoiceSchema)
});

const eventHistorySchema = z.object({
  eventId: z.string().min(1),
  eventTitle: z.string().min(1),
  choiceId: z.string().min(1),
  choiceLabel: z.string().min(1),
  summary: z.string(),
  resolvedAtHour: z.number()
});

const eventCooldownSchema = z.object({
  eventId: z.string().min(1),
  availableAtHour: z.number()
});

const activeTimedModifierSchema = z.object({
  modifierId: z.string().min(1),
  label: z.string().min(1),
  expiresAtHour: z.number(),
  residentialDemandMultiplier: z.number(),
  commercialDemandMultiplier: z.number(),
  industrialDemandMultiplier: z.number(),
  officeDemandMultiplier: z.number(),
  growthMultiplier: z.number(),
  serviceCostMultiplier: z.number(),
  roadMaintenanceMultiplier: z.number(),
  commuteMinutesMultiplier: z.number(),
  taxIncomeMultiplier: z.number()
});

const timedModifierDefinitionStateSchema = z.object({
  id: z.string().min(1),
  label: z.string().min(1),
  durationHours: z.number(),
  residentialDemandMultiplier: z.number(),
  commercialDemandMultiplier: z.number(),
  industrialDemandMultiplier: z.number(),
  officeDemandMultiplier: z.number(),
  growthMultiplier: z.number(),
  serviceCostMultiplier: z.number(),
  roadMaintenanceMultiplier: z.number(),
  commuteMinutesMultiplier: z.number(),
  taxIncomeMultiplier: z.number()
});

const pendingConsequenceSchema = z.object({
  sourceEventId: z.string().min(1),
  consequenceId: z.string().min(1),
  label: z.string().min(1),
  applyAtHour: z.number(),
  followUpEventId: z.string(),
  cashDelta: z.number(),
  loanDelta: z.number(),
  residentialDemandDelta: z.number(),
  commercialDemandDelta: z.number(),
  industrialDemandDelta: z.number(),
  officeDemandDelta: z.number(),
  landValueDelta: z.number(),
  utilityCoverageDelta: z.number(),
  timedModifiers: z.array(timedModifierDefinitionStateSchema)
});

const runStateSchema = z.object({
  currentActIndex: z.number().int().nonnegative(),
  currentActId: z.string().min(1),
  currentActName: z.string().min(1),
  currentActObjective: z.string().min(1),
  currentActProgressValue: z.number().int().nonnegative(),
  currentActProgressTarget: z.number().int().positive(),
  currentActProgress01: z.number(),
  activeEvent: activeCityEventSchema.nullable().optional(),
  eventHistory: z.array(eventHistorySchema),
  eventCooldowns: z.array(eventCooldownSchema),
  activeModifiers: z.array(activeTimedModifierSchema),
  pendingConsequences: z.array(pendingConsequenceSchema).optional().default([]),
  nextEventCheckAtHour: z.number(),
  fiscalDistressHours: z.number(),
  deficitDays: z.number().int().nonnegative(),
  lastDeficitTrackedDay: z.number().int().positive(),
  isGameOver: z.boolean(),
  gameOverReason: z.string(),
  isVictory: z.boolean().optional().default(false),
  victoryReason: z.string().optional().default(""),
  victoryAtHour: z.number().optional().default(0)
});

const demoRunStateSchema = z.object({
  onboardingStepId: z.string().optional().default("onb-01-road"),
  onboardingStepTitle: z.string().optional().default("Apri la rete viaria"),
  onboardingStepInstruction: z.string().optional().default("Traccia la prima strada per connettere i lotti iniziali."),
  onboardingFocusTool: z.string().optional().default("Strada"),
  tutorialEnabled: z.boolean().optional().default(true),
  onboardingStepIndex: z.number().int().nonnegative().optional().default(0),
  onboardingCompletedSteps: z.number().int().nonnegative().optional().default(0),
  onboardingCompleted: z.boolean().optional().default(false),
  softInputLock: z.boolean().optional().default(true),
  currentObjectiveId: z.string().optional().default("act1"),
  currentObjectiveTitle: z.string().optional().default("Fondazione"),
  currentObjectiveTargetPopulation: z.number().int().positive().optional().default(320),
  objectivePopulation: z.number().int().nonnegative().optional().default(0),
  objectiveProgress01: z.number().optional().default(0),
  averageDistrictVitality: z.number().optional().default(0.5),
  economicPressure: z.number().optional().default(0),
  servicePressure: z.number().optional().default(0),
  trafficPressure: z.number().optional().default(0),
  runCompleted: z.boolean().optional().default(false),
  outcome: z.number().int().optional().default(0),
  outcomeReason: z.string().optional().default(""),
  outcomeAtHour: z.number().optional().default(0)
});

const defaultProgressionState = {
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
};

const defaultRunState = {
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
};

const defaultDemoRunState = {
  onboardingStepId: "onb-01-road",
  onboardingStepTitle: "Apri la rete viaria",
  onboardingStepInstruction: "Traccia la prima strada per connettere i lotti iniziali.",
  onboardingFocusTool: "Strada",
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
};

const worldStateSchema = z.object({
  schemaVersion: z.number().int(),
  cityId: z.string().min(1),
  cityName: z.string().min(1),
  tick: z.number().int(),
  nextEntitySequence: z.number().int(),
  time: timeStateSchema,
  budget: budgetStateSchema,
  demand: demandStateSchema,
  utilities: utilityStateSchema,
  roadNodes: z.array(roadNodeSchema),
  roadSegments: z.array(roadSegmentSchema),
  lots: z.array(zoneLotSchema),
  buildings: z.array(buildingStateSchema),
  commuters: z.array(commuterAgentSchema),
  progression: progressionStateSchema.optional().default(defaultProgressionState),
  runState: runStateSchema.optional().default(defaultRunState),
  demoRun: demoRunStateSchema.optional().default(defaultDemoRunState),
  appliedCommandCount: z.number().int(),
  averageCommuteMinutes: z.number(),
  averageTrafficCongestion: z.number()
});

export const snapshotMetadataSchema = z.object({
  sourceClientId: z.string().min(1),
  sourcePlatform: z.string().min(1),
  simulationConfigVersion: z.string().min(1),
  debugLabel: z.string(),
  saveSlotId: z.string().optional().default("autosave"),
  saveReason: z.string().optional().default("manual"),
  lastSyncStatus: z.string().optional().default("pending")
});

export const syncHeadSchema = z.object({
  schemaVersion: z.number().int(),
  cityId: z.string().min(1),
  version: z.string().min(1),
  displayName: z.string().min(1),
  clientId: z.string().min(1),
  commandCount: z.number().int(),
  tick: z.number().int(),
  clientUpdatedAtUtc: z.string().min(1),
  checksum: z.string().min(1)
});

export const rawCitySnapshotSchema = z.object({
  schemaVersion: z.number().int(),
  cityId: z.string().min(1),
  cityName: z.string().min(1),
  version: z.string().min(1),
  createdAtUtc: z.string().min(1).optional(),
  savedAtUtc: z.string().min(1).optional(),
  clientId: z.string().min(1).optional(),
  commandCount: z.number().int().optional(),
  contentHash: z.string().min(1).optional(),
  metadata: snapshotMetadataSchema.optional(),
  state: worldStateSchema
});

export const citySnapshotSchema = rawCitySnapshotSchema.extend({
  createdAtUtc: z.string().min(1),
  savedAtUtc: z.string().min(1),
  clientId: z.string().min(1),
  commandCount: z.number().int(),
  contentHash: z.string().min(1),
  metadata: snapshotMetadataSchema
});

export const loginRequestSchema = z.object({
  username: z.string().min(1),
  password: z.string().min(1)
});

export const refreshRequestSchema = z.object({
  refreshToken: z.string().min(1)
});

export const rawUploadSnapshotRequestSchema = z.object({
  head: syncHeadSchema,
  snapshot: rawCitySnapshotSchema
});

export const uploadSnapshotRequestSchema = z.object({
  head: syncHeadSchema,
  snapshot: citySnapshotSchema
});

export const profileSchema = z.object({
  userId: z.string().min(1),
  username: z.string().min(1),
  activeCityHead: syncHeadSchema.nullable().optional()
});

export const versionManifestSchema = z.object({
  backendApiVersion: z.string().min(1),
  minimumSnapshotSchemaVersion: z.number().int(),
  windowsBuild: z.string().min(1),
  androidBuild: z.string().min(1),
  simulationConfigVersion: z.string().min(1),
  generatedAtUtc: z.string().min(1)
});

export const snapshotWriteReasonSchema = z.enum([
  "applied",
  "duplicate_version",
  "stale_head",
  "checksum_mismatch",
  "version_conflict"
]);

export type CitySnapshot = z.infer<typeof citySnapshotSchema>;
export type RawCitySnapshot = z.infer<typeof rawCitySnapshotSchema>;
export type SyncHead = z.infer<typeof syncHeadSchema>;
export type ProfileState = z.infer<typeof profileSchema>;
export type VersionManifest = z.infer<typeof versionManifestSchema>;
export type SnapshotWriteReason = z.infer<typeof snapshotWriteReasonSchema>;
