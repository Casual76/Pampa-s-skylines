import { readFileSync } from "node:fs";
import path from "node:path";
import { z } from "zod";

export const simulationCatalogManifestSchema = z.object({
  version: z.string().min(1),
  roadsFile: z.string().min(1),
  servicesFile: z.string().min(1),
  zonesFile: z.string().min(1),
  economyFile: z.string().min(1),
  progressionFile: z.string().min(1),
  eventsFile: z.string().min(1)
});

export const roadCatalogSchema = z.object({
  defaultRoadTypeId: z.string().min(1),
  roadTypes: z.array(z.object({
    id: z.string().min(1),
    lanes: z.number().int().positive(),
    capacityPerLane: z.number().int().positive(),
    buildCostPerUnit: z.number().positive(),
    maintenanceCostPerUnit: z.number().nonnegative(),
    refundFactor: z.number().min(0).max(1)
  })).min(1)
});

export const serviceCatalogSchema = z.object({
  serviceDefinitions: z.array(z.object({
    serviceType: z.enum(["Electricity", "Water", "Sewage", "Waste", "Fire", "Police", "Health", "Education"]),
    buildCost: z.number().nonnegative(),
    dailyUpkeep: z.number().nonnegative(),
    defaultCoverageRadius: z.number().positive(),
    refundFactor: z.number().min(0).max(1),
    landValueBonus: z.number(),
    countsAsUtility: z.boolean()
  })).min(1)
});

export const zoneCatalogSchema = z.object({
  zoneDefinitions: z.array(z.object({
    zoneType: z.enum(["Residential", "Commercial", "Industrial", "Office"]),
    spawnGrowthThreshold: z.number().positive(),
    upgradeGrowthThreshold: z.number().positive(),
    maxLevel: z.number().int().positive(),
    baseResidents: z.number().int().nonnegative(),
    baseJobs: z.number().int().nonnegative(),
    upgradeResidents: z.number().int().nonnegative(),
    upgradeJobs: z.number().int().nonnegative()
  })).min(1)
});

export const economyConfigSchema = z.object({
  residentialBaseDemand: z.number(),
  commercialBaseDemand: z.number(),
  industrialBaseDemand: z.number(),
  officeBaseDemand: z.number(),
  zeroPopulationUnemploymentPressure: z.number(),
  commercialPopulationDemandDivisor: z.number().positive(),
  commercialJobSaturationDivisor: z.number().positive(),
  industrialJobSaturationDivisor: z.number().positive(),
  officeJobSaturationDivisor: z.number().positive(),
  officeServiceCoverageWeight: z.number(),
  residentialTaxSensitivity: z.number(),
  commercialTaxSensitivity: z.number(),
  industrialTaxSensitivity: z.number(),
  officeTaxSensitivity: z.number(),
  growthDecayWithoutUtilitiesPerHour: z.number().nonnegative(),
  minimumBudgetDeltaMultiplier: z.number().nonnegative(),
  commuteMinutesPerRoadUnit: z.number().positive(),
  baseLandValue: z.number(),
  roadAccessBonus: z.number(),
  roadAccessPenalty: z.number(),
  electricityBonus: z.number(),
  waterBonus: z.number(),
  sewageBonus: z.number(),
  wasteBonus: z.number(),
  missingWastePenalty: z.number(),
  civicServiceBonusPerCoverage: z.number(),
  districtVitalityGrowthWeight: z.number().default(0.45),
  districtVitalityIncomeWeight: z.number().default(0.35),
  districtVitalityLandValueWeight: z.number().default(0.25),
  strategicTaxSoftThreshold: z.number().default(0.14),
  strategicTaxHardThreshold: z.number().default(0.18),
  strategicTaxDemandPenaltyPerPoint: z.number().default(1.4),
  strategicTaxIncomePenaltyPerPoint: z.number().default(0.8),
  demoTargetPopulation: z.number().int().positive().default(1400),
  victoryMinimumDistrictVitality: z.number().default(0.62),
  victoryMinimumServiceCoverage: z.number().default(0.72),
  victoryMaximumTrafficCongestion: z.number().default(0.62),
  minimumTaxRate: z.number(),
  maximumTaxRate: z.number(),
  minimumTimeScale: z.number(),
  maximumTimeScale: z.number()
});

const progressionMilestoneSchema = z.object({
  id: z.string().min(1),
  displayName: z.string().min(1),
  requiredPopulation: z.number().int().nonnegative(),
  rewardCash: z.number().nonnegative(),
  unlockRoad: z.boolean(),
  unlockBulldoze: z.boolean(),
  unlockBudgetPolicy: z.boolean(),
  unlockZones: z.array(z.enum(["Residential", "Commercial", "Industrial", "Office"])),
  unlockServices: z.array(z.enum(["Electricity", "Water", "Sewage", "Waste", "Fire", "Police", "Health", "Education"]))
});

const bailoutConfigSchema = z.object({
  crisisCashThreshold: z.number(),
  crisisHoursRequired: z.number().positive(),
  cashInjection: z.number().positive(),
  loanIncrease: z.number().positive(),
  cooldownHours: z.number().nonnegative(),
  maxBailouts: z.number().int().positive(),
  dailyRepaymentRate: z.number().positive().max(1)
});

export const progressionCatalogSchema = z.object({
  milestones: z.array(progressionMilestoneSchema).min(1),
  bailout: bailoutConfigSchema
});

const timedModifierSchema = z.object({
  id: z.string().min(1),
  label: z.string().min(1),
  durationHours: z.number().positive(),
  residentialDemandMultiplier: z.number().positive(),
  commercialDemandMultiplier: z.number().positive(),
  industrialDemandMultiplier: z.number().positive(),
  officeDemandMultiplier: z.number().positive(),
  growthMultiplier: z.number().positive(),
  serviceCostMultiplier: z.number().positive(),
  roadMaintenanceMultiplier: z.number().positive(),
  commuteMinutesMultiplier: z.number().positive(),
  taxIncomeMultiplier: z.number().positive()
});

const eventChoiceEffectSchema = z.object({
  cashDelta: z.number(),
  loanDelta: z.number(),
  residentialDemandDelta: z.number(),
  commercialDemandDelta: z.number(),
  industrialDemandDelta: z.number(),
  officeDemandDelta: z.number(),
  landValueDelta: z.number(),
  utilityCoverageDelta: z.number(),
  timedModifiers: z.array(timedModifierSchema),
  delayedConsequences: z.array(z.object({
    id: z.string().min(1),
    label: z.string().min(1),
    delayHours: z.number().positive(),
    followUpEventId: z.string().optional().default(""),
    cashDelta: z.number(),
    loanDelta: z.number(),
    residentialDemandDelta: z.number(),
    commercialDemandDelta: z.number(),
    industrialDemandDelta: z.number(),
    officeDemandDelta: z.number(),
    landValueDelta: z.number(),
    utilityCoverageDelta: z.number(),
    timedModifiers: z.array(timedModifierSchema)
  })).default([])
});

const eventChoiceSchema = z.object({
  id: z.string().min(1),
  label: z.string().min(1),
  description: z.string().min(1),
  effect: eventChoiceEffectSchema
});

const cityEventSchema = z.object({
  id: z.string().min(1),
  title: z.string().min(1),
  description: z.string().min(1),
  weight: z.number().int().positive(),
  minPopulation: z.number().int().nonnegative(),
  maxPopulation: z.number().int().nonnegative(),
  minActIndex: z.number().int().nonnegative(),
  cooldownHours: z.number().nonnegative(),
  choices: z.array(eventChoiceSchema).min(1)
});

const cityActSchema = z.object({
  id: z.string().min(1),
  displayName: z.string().min(1),
  minPopulation: z.number().int().nonnegative(),
  maxPopulation: z.number().int().nonnegative(),
  objectivePopulationTarget: z.number().int().nonnegative(),
  objectiveDescription: z.string().min(1)
});

export const eventsCatalogSchema = z.object({
  spawnIntervalHours: z.number().positive(),
  acts: z.array(cityActSchema).min(1),
  events: z.array(cityEventSchema).min(1)
});

export interface SimulationCatalogBundle {
  manifest: z.infer<typeof simulationCatalogManifestSchema>;
  roads: z.infer<typeof roadCatalogSchema>;
  services: z.infer<typeof serviceCatalogSchema>;
  zones: z.infer<typeof zoneCatalogSchema>;
  economy: z.infer<typeof economyConfigSchema>;
  progression: z.infer<typeof progressionCatalogSchema>;
  events: z.infer<typeof eventsCatalogSchema>;
}

export function loadSimulationCatalogs(rootPath: string): SimulationCatalogBundle {
  const manifest = parseJson(path.join(rootPath, "manifest.json"), simulationCatalogManifestSchema);

  return {
    manifest,
    roads: parseJson(path.join(rootPath, manifest.roadsFile), roadCatalogSchema),
    services: parseJson(path.join(rootPath, manifest.servicesFile), serviceCatalogSchema),
    zones: parseJson(path.join(rootPath, manifest.zonesFile), zoneCatalogSchema),
    economy: parseJson(path.join(rootPath, manifest.economyFile), economyConfigSchema),
    progression: parseJson(path.join(rootPath, manifest.progressionFile), progressionCatalogSchema),
    events: parseJson(path.join(rootPath, manifest.eventsFile), eventsCatalogSchema)
  };
}

export function loadSimulationConfigVersion(rootPath: string): string {
  return loadSimulationCatalogs(rootPath).manifest.version;
}

function parseJson<TSchema extends z.ZodTypeAny>(filePath: string, schema: TSchema): z.output<TSchema> {
  const raw = readFileSync(filePath, "utf8");
  return schema.parse(JSON.parse(raw));
}
