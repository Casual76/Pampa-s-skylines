#nullable enable

namespace PampaSkylines.Simulation
{
using System;
using System.Linq;
using PampaSkylines.Commands;
using PampaSkylines.Core;
using PampaSkylines.Shared;

public static class CommandExecutor
{
    public static CommandExecutionResult Execute(
        WorldState state,
        GameCommand command,
        SimulationConfig config,
        SimulationFrameReport? report = null)
    {
        if (IsBlockedByGameOver(state, command.Type))
        {
            var reason = state.RunState?.IsVictory == true
                ? "Comando bloccato: run demo gia completata."
                : "Comando bloccato: partita terminata per collasso economico.";
            return Reject(command, CommandRejectionReason.GameOverLocked, reason);
        }

        if (IsBlockedByOnboardingSoftLock(state, command.Type, config, out var softLockReason))
        {
            return Reject(command, CommandRejectionReason.ProgressionLocked, softLockReason);
        }

        return command.Type switch
        {
            GameCommandType.BuildRoad => command.BuildRoad is null
                ? Reject(command, CommandRejectionReason.InvalidPayload, "Payload BuildRoad mancante.")
                : BuildRoad(state, command, command.BuildRoad, config),
            GameCommandType.PaintZone => command.PaintZone is null
                ? Reject(command, CommandRejectionReason.InvalidPayload, "Payload PaintZone mancante.")
                : PaintZone(state, command, command.PaintZone, config),
            GameCommandType.PlaceService => command.PlaceService is null
                ? Reject(command, CommandRejectionReason.InvalidPayload, "Payload PlaceService mancante.")
                : PlaceService(state, command, command.PlaceService, config),
            GameCommandType.Bulldoze => command.Bulldoze is null
                ? Reject(command, CommandRejectionReason.InvalidPayload, "Payload Bulldoze mancante.")
                : Bulldoze(state, command, command.Bulldoze, config),
            GameCommandType.UpdateBudgetPolicy => command.BudgetPolicy is null
                ? Reject(command, CommandRejectionReason.InvalidPayload, "Payload BudgetPolicy mancante.")
                : UpdateBudget(state, command, command.BudgetPolicy, config),
            GameCommandType.SetTimeScale => command.TimeControl is null
                ? Reject(command, CommandRejectionReason.InvalidPayload, "Payload TimeControl mancante.")
                : UpdateTime(state, command, command.TimeControl, config),
            GameCommandType.ResolveEventChoice => command.ResolveEventChoice is null
                ? Reject(command, CommandRejectionReason.InvalidPayload, "Payload ResolveEventChoice mancante.")
                : ResolveEventChoice(state, command, command.ResolveEventChoice, config, report),
            _ => Reject(command, CommandRejectionReason.InvalidPayload, "Tipo comando non supportato.")
        };
    }

    private static CommandExecutionResult BuildRoad(WorldState state, GameCommand command, BuildRoadCommandData payload, SimulationConfig config)
    {
        if (!config.IsRoadUnlocked(state.Progression))
        {
            return Reject(command, CommandRejectionReason.ProgressionLocked, "Strumento strada bloccato dalla progressione.");
        }

        if (payload.Start.Equals(payload.End))
        {
            return Reject(command, CommandRejectionReason.InvalidPayload, "Inizio e fine strada non possono coincidere.");
        }

        if (!config.SupportsRoadType(payload.RoadTypeId, Math.Max(1, payload.Lanes)))
        {
            return Reject(command, CommandRejectionReason.UnsupportedRoadType, "Tipo strada non supportato dalla configurazione attiva.");
        }

        if (HasRoadBetween(state, payload.Start, payload.End))
        {
            return Reject(command, CommandRejectionReason.DuplicateRoad, "Segmento strada gia esistente.");
        }

        var roadDefinition = config.ResolveRoadType(payload.RoadTypeId, Math.Max(1, payload.Lanes));
        var buildCost = EstimateRoadCost(payload.Start, payload.End, roadDefinition);
        if (!TrySpend(state, buildCost))
        {
            return Reject(command, CommandRejectionReason.InsufficientFunds, "Cassa insufficiente per costruire la strada.");
        }

        var startNode = GetOrCreateNode(state, payload.Start);
        var endNode = GetOrCreateNode(state, payload.End);
        state.RoadSegments.Add(new RoadSegment
        {
            Id = DeterministicIdGenerator.Next(state, "road-segment"),
            RoadTypeId = roadDefinition.Id,
            FromNodeId = startNode.Id,
            ToNodeId = endNode.Id,
            Lanes = roadDefinition.Lanes,
            Capacity = roadDefinition.CapacityPerLane * roadDefinition.Lanes,
            Length = payload.Start.EuclideanDistance(payload.End)
        });

        return Applied(command, $"Strada '{roadDefinition.Id}' costruita.", -buildCost, state.Tick);
    }

    private static CommandExecutionResult PaintZone(WorldState state, GameCommand command, ZonePaintCommandData payload, SimulationConfig config)
    {
        if (payload.Cells.Count == 0)
        {
            return Reject(command, CommandRejectionReason.InvalidPayload, "PaintZone richiede almeno una cella.");
        }

        if (!config.IsZoneUnlocked(payload.ZoneType, state.Progression))
        {
            return Reject(command, CommandRejectionReason.ProgressionLocked, $"Zona '{payload.ZoneType}' bloccata dalla progressione.");
        }

        if (!config.SupportsZone(payload.ZoneType))
        {
            return Reject(command, CommandRejectionReason.UnsupportedZoneType, "Tipo zona non supportato dalla configurazione attiva.");
        }

        foreach (var cell in payload.Cells.OrderBy(static cell => cell.X).ThenBy(static cell => cell.Y))
        {
            var lot = state.Lots.FirstOrDefault(existing => existing.Cell.Equals(cell));
            if (lot is null)
            {
                lot = new ZoneLot
                {
                    Id = DeterministicIdGenerator.Next(state, "lot"),
                    Cell = cell,
                    ZoneType = payload.ZoneType
                };

                state.Lots.Add(lot);
            }
            else
            {
                lot.ZoneType = payload.ZoneType;
            }
        }

        return Applied(command, "Zonizzazione applicata.", 0m, state.Tick);
    }

    private static CommandExecutionResult PlaceService(WorldState state, GameCommand command, PlaceServiceCommandData payload, SimulationConfig config)
    {
        if (!config.IsServiceUnlocked(payload.ServiceType, state.Progression))
        {
            return Reject(command, CommandRejectionReason.ProgressionLocked, $"Servizio '{payload.ServiceType}' bloccato dalla progressione.");
        }

        if (!config.SupportsService(payload.ServiceType))
        {
            return Reject(command, CommandRejectionReason.UnsupportedServiceType, "Tipo servizio non supportato dalla configurazione attiva.");
        }

        var service = config.ResolveService(payload.ServiceType);
        var lot = state.Lots.FirstOrDefault(existing => existing.Cell.Equals(payload.Cell));
        if (lot is null)
        {
            lot = new ZoneLot
            {
                Id = DeterministicIdGenerator.Next(state, "lot"),
                Cell = payload.Cell,
                ZoneType = ZoneType.None,
                HasRoadAccess = true
            };

            state.Lots.Add(lot);
        }

        var existingBuilding = state.Buildings.FirstOrDefault(existing => existing.LotId == lot.Id);
        if (existingBuilding is not null && existingBuilding.ServiceType == payload.ServiceType)
        {
            existingBuilding.CoverageRadius = payload.CoverageRadius > 0f ? payload.CoverageRadius : service.DefaultCoverageRadius;
            return Applied(command, "Copertura servizio aggiornata.", 0m, state.Tick);
        }

        if (!TrySpend(state, service.BuildCost))
        {
            return Reject(command, CommandRejectionReason.InsufficientFunds, "Cassa insufficiente per posizionare il servizio.");
        }

        if (existingBuilding is not null)
        {
            existingBuilding.ServiceType = payload.ServiceType;
            existingBuilding.CoverageRadius = payload.CoverageRadius > 0f ? payload.CoverageRadius : service.DefaultCoverageRadius;
            return Applied(command, "Servizio convertito sul lotto esistente.", -service.BuildCost, state.Tick);
        }

        state.Buildings.Add(new BuildingState
        {
            Id = DeterministicIdGenerator.Next(state, "building"),
            LotId = lot.Id,
            Cell = payload.Cell,
            ZoneType = ZoneType.None,
            ServiceType = payload.ServiceType,
            CoverageRadius = payload.CoverageRadius > 0f ? payload.CoverageRadius : service.DefaultCoverageRadius
        });

        return Applied(command, "Edificio servizio posizionato.", -service.BuildCost, state.Tick);
    }

    private static CommandExecutionResult Bulldoze(WorldState state, GameCommand command, BulldozeCommandData payload, SimulationConfig config)
    {
        if (!config.IsBulldozeUnlocked(state.Progression))
        {
            return Reject(command, CommandRejectionReason.ProgressionLocked, "Bulldozer bloccato dalla progressione.");
        }

        if (string.IsNullOrWhiteSpace(payload.RoadSegmentId) &&
            string.IsNullOrWhiteSpace(payload.LotId) &&
            string.IsNullOrWhiteSpace(payload.BuildingId))
        {
            return Reject(command, CommandRejectionReason.InvalidPayload, "Bulldozer richiede un bersaglio.");
        }

        decimal refundTotal = 0m;
        var anyRemoved = false;

        if (!string.IsNullOrWhiteSpace(payload.RoadSegmentId))
        {
            var removedSegments = state.RoadSegments
                .Where(segment => segment.Id == payload.RoadSegmentId)
                .ToList();

            state.RoadSegments.RemoveAll(segment => segment.Id == payload.RoadSegmentId);
            foreach (var segment in removedSegments)
            {
                var roadDefinition = config.ResolveRoadType(segment.RoadTypeId, segment.Lanes);
                refundTotal += GetRoadRefund(segment, roadDefinition);
            }

            anyRemoved |= removedSegments.Count > 0;
        }

        if (!string.IsNullOrWhiteSpace(payload.BuildingId))
        {
            var removedBuildings = state.Buildings
                .Where(building => building.Id == payload.BuildingId)
                .ToList();

            state.Buildings.RemoveAll(building => building.Id == payload.BuildingId);
            state.Commuters.RemoveAll(agent => agent.HomeBuildingId == payload.BuildingId || agent.WorkBuildingId == payload.BuildingId);

            foreach (var building in removedBuildings)
            {
                refundTotal += GetBuildingRefund(config, building);
            }

            anyRemoved |= removedBuildings.Count > 0;
        }

        if (!string.IsNullOrWhiteSpace(payload.LotId))
        {
            var removedLots = state.Lots
                .Where(lot => lot.Id == payload.LotId)
                .ToList();

            var lotBuildings = state.Buildings
                .Where(building => building.LotId == payload.LotId)
                .ToList();

            state.Buildings.RemoveAll(building => building.LotId == payload.LotId);
            state.Lots.RemoveAll(lot => lot.Id == payload.LotId);
            foreach (var building in lotBuildings)
            {
                refundTotal += GetBuildingRefund(config, building);
            }

            anyRemoved |= lotBuildings.Count > 0 || removedLots.Count > 0;
        }

        if (!anyRemoved)
        {
            return Reject(command, CommandRejectionReason.TargetNotFound, "Nessun elemento corrisponde al bersaglio bulldozer.");
        }

        Refund(state, refundTotal);
        return Applied(command, "Demolizione applicata.", refundTotal, state.Tick);
    }

    private static CommandExecutionResult UpdateBudget(WorldState state, GameCommand command, BudgetPolicyCommandData payload, SimulationConfig config)
    {
        if (!config.IsBudgetPolicyUnlocked(state.Progression))
        {
            return Reject(command, CommandRejectionReason.ProgressionLocked, "Politiche bilancio bloccate dalla progressione.");
        }

        if (!IsValidTax(payload.ResidentialTaxRate, config) ||
            !IsValidTax(payload.CommercialTaxRate, config) ||
            !IsValidTax(payload.IndustrialTaxRate, config) ||
            !IsValidTax(payload.OfficeTaxRate, config))
        {
            return Reject(command, CommandRejectionReason.InvalidTaxRate, "Una o piu aliquote sono fuori dal range consentito.");
        }

        if (payload.ResidentialTaxRate.HasValue)
        {
            state.Budget.TaxRateResidential = payload.ResidentialTaxRate.Value;
        }

        if (payload.CommercialTaxRate.HasValue)
        {
            state.Budget.TaxRateCommercial = payload.CommercialTaxRate.Value;
        }

        if (payload.IndustrialTaxRate.HasValue)
        {
            state.Budget.TaxRateIndustrial = payload.IndustrialTaxRate.Value;
        }

        if (payload.OfficeTaxRate.HasValue)
        {
            state.Budget.TaxRateOffice = payload.OfficeTaxRate.Value;
        }

        return Applied(command, "Politiche di bilancio aggiornate.", 0m, state.Tick);
    }

    private static CommandExecutionResult UpdateTime(WorldState state, GameCommand command, TimeControlCommandData payload, SimulationConfig config)
    {
        if (payload.SpeedMultiplier.HasValue &&
            (payload.SpeedMultiplier.Value < config.Economy.MinimumTimeScale || payload.SpeedMultiplier.Value > config.Economy.MaximumTimeScale))
        {
            return Reject(command, CommandRejectionReason.InvalidTimeScale, "Velocita tempo fuori range consentito.");
        }

        if (payload.IsPaused.HasValue)
        {
            state.Time.IsPaused = payload.IsPaused.Value;
        }

        if (payload.SpeedMultiplier.HasValue)
        {
            state.Time.SpeedMultiplier = payload.SpeedMultiplier.Value;
        }

        return Applied(command, "Controlli tempo aggiornati.", 0m, state.Tick);
    }

    private static CommandExecutionResult ResolveEventChoice(
        WorldState state,
        GameCommand command,
        ResolveEventChoiceCommandData payload,
        SimulationConfig config,
        SimulationFrameReport? report)
    {
        var resolution = CityEventModel.ResolveActiveEventChoice(
            state,
            config,
            payload.EventId,
            payload.ChoiceId,
            report);

        if (!resolution.Applied)
        {
            return Reject(command, resolution.RejectionReason, resolution.Message);
        }

        return Applied(command, resolution.Message, 0m, state.Tick);
    }

    private static bool IsBlockedByGameOver(WorldState state, GameCommandType commandType)
    {
        if (state.RunState?.IsGameOver != true && state.RunState?.IsVictory != true)
        {
            return false;
        }

        return commandType is GameCommandType.BuildRoad
            or GameCommandType.PaintZone
            or GameCommandType.PlaceService
            or GameCommandType.Bulldoze
            or GameCommandType.UpdateBudgetPolicy;
    }

    private static bool IsBlockedByOnboardingSoftLock(
        WorldState state,
        GameCommandType commandType,
        SimulationConfig config,
        out string reason)
    {
        reason = string.Empty;
        var demo = state.DemoRun;
        if (demo is null || !demo.TutorialEnabled || !demo.SoftInputLock || demo.OnboardingCompleted)
        {
            return false;
        }

        if (DemoOnboardingGuide.IsCommandAllowedDuringSoftLock(commandType, demo.OnboardingCompletedSteps))
        {
            return false;
        }

        reason = DemoOnboardingGuide.BuildSoftLockReason(demo.OnboardingCompletedSteps, config);
        return true;
    }

    private static bool HasRoadBetween(WorldState state, Int2 start, Int2 end)
    {
        return state.RoadSegments.Any(segment =>
        {
            var from = state.RoadNodes.FirstOrDefault(node => node.Id == segment.FromNodeId);
            var to = state.RoadNodes.FirstOrDefault(node => node.Id == segment.ToNodeId);
            if (from is null || to is null)
            {
                return false;
            }

            return (from.Position.Equals(start) && to.Position.Equals(end))
                || (from.Position.Equals(end) && to.Position.Equals(start));
        });
    }

    private static RoadNode GetOrCreateNode(WorldState state, Int2 position)
    {
        var node = state.RoadNodes.FirstOrDefault(existing => existing.Position.Equals(position));
        if (node is not null)
        {
            return node;
        }

        node = new RoadNode
        {
            Id = DeterministicIdGenerator.Next(state, "road-node"),
            Position = position
        };

        state.RoadNodes.Add(node);
        return node;
    }

    private static bool TrySpend(WorldState state, decimal amount)
    {
        if (amount <= 0)
        {
            return true;
        }

        if (state.Budget.Cash < amount)
        {
            return false;
        }

        state.Budget.Cash -= amount;
        state.Budget.DailyConstructionCost += amount;
        return true;
    }

    private static void Refund(WorldState state, decimal amount)
    {
        if (amount <= 0)
        {
            return;
        }

        state.Budget.Cash += amount;
        state.Budget.DailyConstructionCost = Math.Max(0m, state.Budget.DailyConstructionCost - amount);
    }

    private static decimal EstimateRoadCost(Int2 start, Int2 end, RoadTypeDefinition roadDefinition)
    {
        var length = (decimal)start.EuclideanDistance(end);
        return Math.Round(length * roadDefinition.BuildCostPerUnit, 2);
    }

    private static decimal GetRoadRefund(RoadSegment segment, RoadTypeDefinition roadDefinition)
    {
        return Math.Round((decimal)segment.Length * roadDefinition.BuildCostPerUnit * roadDefinition.RefundFactor, 2);
    }

    private static decimal GetBuildingRefund(SimulationConfig config, BuildingState building)
    {
        if (building.ServiceType == ServiceType.None || !config.SupportsService(building.ServiceType))
        {
            return 0m;
        }

        var service = config.ResolveService(building.ServiceType);
        return Math.Round(service.BuildCost * service.RefundFactor, 2);
    }

    private static bool IsValidTax(decimal? value, SimulationConfig config)
    {
        return !value.HasValue || (value.Value >= config.Economy.MinimumTaxRate && value.Value <= config.Economy.MaximumTaxRate);
    }

    private static CommandExecutionResult Applied(GameCommand command, string message, decimal cashDelta, long tick)
    {
        return new CommandExecutionResult
        {
            CommandId = command.CommandId,
            Type = command.Type,
            Status = CommandExecutionStatus.Applied,
            RejectionReason = CommandRejectionReason.None,
            Message = message,
            CashDelta = cashDelta,
            AppliedAtTick = tick
        };
    }

    private static CommandExecutionResult Reject(GameCommand command, CommandRejectionReason reason, string message)
    {
        return new CommandExecutionResult
        {
            CommandId = command.CommandId,
            Type = command.Type,
            Status = CommandExecutionStatus.Rejected,
            RejectionReason = reason,
            Message = message,
            AppliedAtTick = -1
        };
    }
}
}
