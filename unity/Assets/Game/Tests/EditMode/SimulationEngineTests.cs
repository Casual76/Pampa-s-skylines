namespace PampaSkylines.Tests
{
using System.Collections.Generic;
using NUnit.Framework;
using PampaSkylines.Commands;
using PampaSkylines.Core;
using PampaSkylines.SaveSync;
using PampaSkylines.Shared;
using PampaSkylines.Simulation;

public sealed class SimulationEngineTests
{
    [Test]
    public void SimulationTick_GrowsResidential_WhenRoadAndUtilitiesExist()
    {
        var state = WorldState.CreateNew("Test City");
        var commands = new CommandBuffer();
        var config = SimulationConfig.CreateFallback();

        commands.Enqueue(new GameCommand
        {
            Type = GameCommandType.BuildRoad,
            BuildRoad = new BuildRoadCommandData
            {
                Start = new Int2(0, 0),
                End = new Int2(3, 0)
            }
        });

        commands.Enqueue(new GameCommand
        {
            Type = GameCommandType.PaintZone,
            PaintZone = new ZonePaintCommandData
            {
                ZoneType = ZoneType.Residential,
                Cells = { new Int2(1, 1) }
            }
        });

        commands.Enqueue(new GameCommand
        {
            Type = GameCommandType.PlaceService,
            PlaceService = new PlaceServiceCommandData
            {
                ServiceType = ServiceType.Electricity,
                Cell = new Int2(0, 1)
            }
        });

        commands.Enqueue(new GameCommand
        {
            Type = GameCommandType.PlaceService,
            PlaceService = new PlaceServiceCommandData
            {
                ServiceType = ServiceType.Water,
                Cell = new Int2(1, 0)
            }
        });

        commands.Enqueue(new GameCommand
        {
            Type = GameCommandType.PlaceService,
            PlaceService = new PlaceServiceCommandData
            {
                ServiceType = ServiceType.Sewage,
                Cell = new Int2(2, 0)
            }
        });

        for (var index = 0; index < 8; index++)
        {
            SimulationEngine.SimulationStep(state, commands, 0.5f, config);
        }

        Assert.That(state.Buildings.Exists(building => building.ZoneType == ZoneType.Residential), Is.True);
        Assert.That(state.Population, Is.GreaterThan(0));
    }

    [Test]
    public void CitySaveCodec_RoundTripsSnapshot()
    {
        var state = WorldState.CreateNew("Save Test");
        var snapshot = CitySnapshot.FromWorld(state, "v-test", "pc-test");

        var payload = CitySaveCodec.Encode(snapshot);
        var restored = CitySaveCodec.Decode(payload);

        Assert.That(restored.CityId, Is.EqualTo(snapshot.CityId));
        Assert.That(restored.State.CityName, Is.EqualTo("Save Test"));
        Assert.That(restored.ContentHash, Is.Not.Empty);
    }

    [Test]
    public void SimulationTick_SpendsBudget_ForRoadAndServiceConstruction()
    {
        var state = WorldState.CreateNew("Budget Test");
        var commands = new CommandBuffer();
        var config = SimulationConfig.CreateFallback();
        state.Progression = ProgressionState.CreateForPopulation(config.Progression, 5000, treatRewardsAsAlreadyGranted: true);
        var startingCash = state.Budget.Cash;

        commands.Enqueue(new GameCommand
        {
            Type = GameCommandType.BuildRoad,
            BuildRoad = new BuildRoadCommandData
            {
                Start = new Int2(0, 0),
                End = new Int2(6, 0),
                Lanes = 2
            }
        });

        commands.Enqueue(new GameCommand
        {
            Type = GameCommandType.PlaceService,
            PlaceService = new PlaceServiceCommandData
            {
                ServiceType = ServiceType.Fire,
                Cell = new Int2(1, 1)
            }
        });

        SimulationEngine.SimulationStep(state, commands, 0.5f, config);

        Assert.That(state.Budget.Cash, Is.LessThan(startingCash));
        Assert.That(state.Budget.DailyServiceCost, Is.GreaterThan(0m));
    }

    [Test]
    public void SimulationTick_CreatesCommuters_WhenHomesJobsAndConnectedRoadsExist()
    {
        var state = WorldState.CreateNew("Traffic Test");
        var commands = new CommandBuffer();
        var config = SimulationConfig.CreateFallback();
        state.Progression = ProgressionState.CreateForPopulation(config.Progression, 5000, treatRewardsAsAlreadyGranted: true);

        commands.Enqueue(new GameCommand
        {
            Type = GameCommandType.BuildRoad,
            BuildRoad = new BuildRoadCommandData
            {
                Start = new Int2(0, 0),
                End = new Int2(2, 0)
            }
        });

        commands.Enqueue(new GameCommand
        {
            Type = GameCommandType.BuildRoad,
            BuildRoad = new BuildRoadCommandData
            {
                Start = new Int2(2, 0),
                End = new Int2(4, 0)
            }
        });

        commands.Enqueue(new GameCommand
        {
            Type = GameCommandType.PaintZone,
            PaintZone = new ZonePaintCommandData
            {
                ZoneType = ZoneType.Residential,
                Cells = { new Int2(1, 1) }
            }
        });

        commands.Enqueue(new GameCommand
        {
            Type = GameCommandType.PaintZone,
            PaintZone = new ZonePaintCommandData
            {
                ZoneType = ZoneType.Industrial,
                Cells = { new Int2(3, 1) }
            }
        });

        commands.Enqueue(new GameCommand
        {
            Type = GameCommandType.PlaceService,
            PlaceService = new PlaceServiceCommandData
            {
                ServiceType = ServiceType.Electricity,
                Cell = new Int2(0, 1)
            }
        });

        commands.Enqueue(new GameCommand
        {
            Type = GameCommandType.PlaceService,
            PlaceService = new PlaceServiceCommandData
            {
                ServiceType = ServiceType.Water,
                Cell = new Int2(2, 1)
            }
        });

        commands.Enqueue(new GameCommand
        {
            Type = GameCommandType.PlaceService,
            PlaceService = new PlaceServiceCommandData
            {
                ServiceType = ServiceType.Sewage,
                Cell = new Int2(4, 1)
            }
        });

        for (var index = 0; index < 12; index++)
        {
            SimulationEngine.SimulationStep(state, commands, 0.5f, config);
        }

        Assert.That(state.Commuters.Count, Is.GreaterThan(0));
        Assert.That(state.AverageCommuteMinutes, Is.GreaterThan(0f));
    }

    [Test]
    public void SimulationStep_ReplaysDeterministically_WithSameCommandSequence()
    {
        var config = SimulationConfig.CreateFallback();
        var directState = WorldState.CreateNew("Replay Test");
        directState.CityId = "replay-city";
        var replayState = WorldState.CreateNew("Replay Test");
        replayState.CityId = "replay-city";
        var buffer = new CommandBuffer();
        var replayCommands = new[]
        {
            new GameCommand
            {
                Type = GameCommandType.BuildRoad,
                ClientId = "pc",
                ClientSequence = 1,
                BuildRoad = new BuildRoadCommandData
                {
                    Start = new Int2(0, 0),
                    End = new Int2(4, 0),
                    RoadTypeId = "road-2lane"
                }
            },
            new GameCommand
            {
                Type = GameCommandType.PaintZone,
                ClientId = "pc",
                ClientSequence = 2,
                PaintZone = new ZonePaintCommandData
                {
                    ZoneType = ZoneType.Residential,
                    Cells = { new Int2(1, 1) }
                }
            }
        };

        foreach (var command in replayCommands)
        {
            buffer.Enqueue(command);
        }

        var directReport = SimulationEngine.SimulationStep(directState, buffer, 0.5f, config);
        var replayResult = CommandReplayEngine.Replay(
            replayState,
            new CommandReplayLog
            {
                CityId = "replay-city",
                ClientId = "pc",
                SimulationConfigVersion = config.Version,
                FixedDeltaTime = 0.5f,
                Commands = { replayCommands[0], replayCommands[1] }
            },
            config);

        Assert.That(replayResult.FinalStateHash, Is.EqualTo(directReport.StateHashAfter));
    }

    [Test]
    public void SimulationStep_RejectsInvalidTaxRate()
    {
        var config = SimulationConfig.CreateFallback();
        var state = WorldState.CreateNew("Invalid Budget");
        state.Progression = ProgressionState.CreateForPopulation(config.Progression, 5000, treatRewardsAsAlreadyGranted: true);
        var commands = new CommandBuffer();

        commands.Enqueue(new GameCommand
        {
            Type = GameCommandType.UpdateBudgetPolicy,
            BudgetPolicy = new BudgetPolicyCommandData
            {
                ResidentialTaxRate = 0.90m
            }
        });

        var report = SimulationEngine.SimulationStep(state, commands, 0.5f, config);

        Assert.That(report.RejectedCommandCount, Is.EqualTo(1));
        Assert.That(state.Budget.TaxRateResidential, Is.EqualTo(0.11m));
    }

    [Test]
    public void SimulationStep_DoesNotAdvanceTimeOrGrowth_WhenPaused()
    {
        var config = SimulationConfig.CreateFallback();
        var state = WorldState.CreateNew("Paused Test");
        state.Time.IsPaused = true;

        SimulationEngine.SimulationStep(state, CreateResidentialBootstrapCommands(), 0.5f, config);

        var lot = state.Lots.Find(existing => existing.Cell.Equals(new Int2(1, 1)));
        Assert.That(state.Time.TimeOfDayHours, Is.EqualTo(8f));
        Assert.That(lot, Is.Not.Null);
        Assert.That(lot!.GrowthProgress, Is.EqualTo(0f));
        Assert.That(state.Buildings.Exists(building => building.ZoneType == ZoneType.Residential), Is.False);
    }

    [Test]
    public void SimulationStep_ScalesGrowthAndClock_WithTimeScale()
    {
        var config = SimulationConfig.CreateFallback();
        var normalState = WorldState.CreateNew("Normal Time");
        var fastState = WorldState.CreateNew("Fast Time");
        fastState.Time.SpeedMultiplier = 2f;

        SimulationEngine.SimulationStep(normalState, CreateResidentialBootstrapCommands(), 0.5f, config);
        SimulationEngine.SimulationStep(fastState, CreateResidentialBootstrapCommands(), 0.5f, config);

        var normalLot = normalState.Lots.Find(existing => existing.Cell.Equals(new Int2(1, 1)));
        var fastLot = fastState.Lots.Find(existing => existing.Cell.Equals(new Int2(1, 1)));

        Assert.That(normalLot, Is.Not.Null);
        Assert.That(fastLot, Is.Not.Null);
        Assert.That(fastState.Time.TimeOfDayHours, Is.GreaterThan(normalState.Time.TimeOfDayHours));
        Assert.That(fastLot!.GrowthProgress, Is.GreaterThan(normalLot!.GrowthProgress));
    }

    [Test]
    public void SimulationStep_RejectsLockedService_BeforeMilestoneUnlock()
    {
        var config = SimulationConfig.CreateFallback();
        var state = WorldState.CreateNew("Progression Lock");
        var commands = new CommandBuffer();
        commands.Enqueue(new GameCommand
        {
            Type = GameCommandType.PlaceService,
            PlaceService = new PlaceServiceCommandData
            {
                ServiceType = ServiceType.Fire,
                Cell = new Int2(0, 0)
            }
        });

        var report = SimulationEngine.SimulationStep(state, commands, 0.5f, config);

        Assert.That(report.RejectedCommandCount, Is.EqualTo(1));
        Assert.That(report.CommandResults[0].RejectionReason, Is.EqualTo(CommandRejectionReason.ProgressionLocked));
    }

    [Test]
    public void SimulationStep_UnlocksMilestoneAndAwardsReward_WhenPopulationReachesTarget()
    {
        var config = SimulationConfig.CreateFallback();
        var state = WorldState.CreateNew("Milestone Unlock");
        var commands = new CommandBuffer();

        commands.Enqueue(new GameCommand
        {
            Type = GameCommandType.BuildRoad,
            BuildRoad = new BuildRoadCommandData
            {
                Start = new Int2(0, 0),
                End = new Int2(8, 0)
            }
        });

        commands.Enqueue(new GameCommand
        {
            Type = GameCommandType.PaintZone,
            PaintZone = new ZonePaintCommandData
            {
                ZoneType = ZoneType.Residential,
                Cells =
                {
                    new Int2(1, 1), new Int2(2, 1), new Int2(3, 1), new Int2(4, 1),
                    new Int2(5, 1), new Int2(6, 1), new Int2(7, 1), new Int2(8, 1)
                }
            }
        });

        commands.Enqueue(new GameCommand
        {
            Type = GameCommandType.PlaceService,
            PlaceService = new PlaceServiceCommandData
            {
                ServiceType = ServiceType.Electricity,
                Cell = new Int2(0, 1)
            }
        });

        commands.Enqueue(new GameCommand
        {
            Type = GameCommandType.PlaceService,
            PlaceService = new PlaceServiceCommandData
            {
                ServiceType = ServiceType.Water,
                Cell = new Int2(2, 0)
            }
        });

        commands.Enqueue(new GameCommand
        {
            Type = GameCommandType.PlaceService,
            PlaceService = new PlaceServiceCommandData
            {
                ServiceType = ServiceType.Sewage,
                Cell = new Int2(4, 0)
            }
        });

        for (var index = 0; index < 240 && state.Population < 80; index++)
        {
            SimulationEngine.SimulationStep(state, commands, 0.5f, config);
        }

        Assert.That(state.Population, Is.GreaterThanOrEqualTo(80));
        Assert.That(state.Progression.ReachedMilestoneIds.Contains("m1"), Is.True);
        Assert.That(state.Progression.TotalMilestoneRewardsAwarded, Is.GreaterThanOrEqualTo(8000m));
    }

    [Test]
    public void SimulationStep_TriggersBailout_WhenCrisisPersists()
    {
        var config = SimulationConfig.CreateFallback();
        var state = WorldState.CreateNew("Bailout Test");
        state.Budget.Cash = -6000m;
        var commands = new CommandBuffer();

        for (var index = 0; index < 8; index++)
        {
            SimulationEngine.SimulationStep(state, commands, 1f, config);
        }

        Assert.That(state.Progression.BailoutCount, Is.EqualTo(1));
        Assert.That(state.Budget.Cash, Is.GreaterThan(-6000m));
        Assert.That(state.Budget.LoanBalance, Is.GreaterThan(0m));
    }

    [Test]
    public void CitySnapshotMigrator_AssignsProgressionDefaults_ForLegacySnapshot()
    {
        var legacyState = WorldState.CreateNew("Legacy");
        legacyState.SchemaVersion = 1;
        legacyState.Progression = null!;

        var snapshot = new CitySnapshot
        {
            SchemaVersion = 1,
            CityId = legacyState.CityId,
            CityName = legacyState.CityName,
            Version = "legacy-v1",
            State = legacyState
        };

        var migrated = CitySnapshotMigrator.MigrateToCurrent(snapshot);
        Assert.That(migrated.SchemaVersion, Is.EqualTo(4));
        Assert.That(migrated.State.SchemaVersion, Is.EqualTo(4));
        Assert.That(migrated.State.Progression, Is.Not.Null);
        Assert.That(migrated.State.Progression.RoadUnlocked, Is.True);
        Assert.That(migrated.State.Progression.IsServiceUnlocked(ServiceType.Electricity), Is.True);
        Assert.That(migrated.State.RunState, Is.Not.Null);
        Assert.That(migrated.State.RunState.CurrentActId, Is.EqualTo("act1"));
    }

    [Test]
    public void SimulationStep_BailoutHonorsCooldownAndMaxCount()
    {
        var config = SimulationConfig.CreateFallback();
        var state = WorldState.CreateNew("Bailout Cooldown");
        var commands = new CommandBuffer();

        state.Budget.Cash = -6000m;
        for (var index = 0; index < 8; index++)
        {
            SimulationEngine.SimulationStep(state, commands, 1f, config);
        }

        Assert.That(state.Progression.BailoutCount, Is.EqualTo(1));

        state.Budget.Cash = -6000m;
        for (var index = 0; index < 10; index++)
        {
            SimulationEngine.SimulationStep(state, commands, 1f, config);
        }

        Assert.That(state.Progression.BailoutCount, Is.EqualTo(1), "Cooldown should block immediate second bailout.");

        state.Budget.Cash = -6000m;
        for (var index = 0; index < 50; index++)
        {
            SimulationEngine.SimulationStep(state, commands, 1f, config);
        }

        Assert.That(state.Progression.BailoutCount, Is.EqualTo(2));

        state.Budget.Cash = -6000m;
        for (var index = 0; index < 50; index++)
        {
            SimulationEngine.SimulationStep(state, commands, 1f, config);
        }

        Assert.That(state.Progression.BailoutCount, Is.EqualTo(3));

        state.Budget.Cash = -6000m;
        for (var index = 0; index < 60; index++)
        {
            SimulationEngine.SimulationStep(state, commands, 1f, config);
        }

        Assert.That(state.Progression.BailoutCount, Is.EqualTo(3), "Bailout count should stop at configured maximum.");
    }

    [Test]
    public void SimulationStep_RepaysLoanAutomaticallyEachDay()
    {
        var config = SimulationConfig.CreateFallback();
        var state = WorldState.CreateNew("Loan Repayment");
        var commands = new CommandBuffer();

        state.Budget.Cash = 5000m;
        state.Budget.LoanBalance = 10000m;
        state.Progression.LastLoanRepaymentDay = state.Time.Day;

        SimulationEngine.SimulationStep(state, commands, 16f, config);

        Assert.That(state.Time.Day, Is.EqualTo(2));
        Assert.That(state.Budget.LoanBalance, Is.EqualTo(9600m));
        Assert.That(state.Budget.Cash, Is.EqualTo(4600m));
    }

    [Test]
    public void CitySaveCodec_RoundTripPreservesProgressionAndLoanState()
    {
        var state = WorldState.CreateNew("RoundTrip Progression");
        state.Progression.CurrentMilestoneIndex = 3;
        state.Progression.CurrentMilestoneId = "m3";
        state.Progression.CurrentMilestoneName = "Municipio";
        state.Progression.BudgetPolicyUnlocked = true;
        state.Progression.BailoutCount = 2;
        state.Progression.NextBailoutAvailableAtHour = 96f;
        state.Progression.LastLoanRepaymentDay = 6;
        state.Budget.LoanBalance = 24000m;
        state.Time.Day = 6;
        state.RunState.CurrentActIndex = 2;
        state.RunState.CurrentActId = "act3";
        state.RunState.CurrentActName = "Pressione Metropolitana";
        state.RunState.FiscalDistressHours = 5f;
        state.RunState.DeficitDays = 2;

        var snapshot = CitySnapshot.FromWorld(state, "v3-test", "pc-test");
        var encoded = CitySaveCodec.Encode(snapshot);
        var restored = CitySaveCodec.Decode(encoded);

        Assert.That(restored.State.SchemaVersion, Is.EqualTo(4));
        Assert.That(restored.State.Progression.CurrentMilestoneId, Is.EqualTo("m3"));
        Assert.That(restored.State.Progression.BailoutCount, Is.EqualTo(2));
        Assert.That(restored.State.Progression.BudgetPolicyUnlocked, Is.True);
        Assert.That(restored.State.Budget.LoanBalance, Is.EqualTo(24000m));
        Assert.That(restored.State.RunState.CurrentActId, Is.EqualTo("act3"));
        Assert.That(restored.State.RunState.DeficitDays, Is.EqualTo(2));
    }

    [Test]
    public void SimulationStep_OnboardingSoftLock_RejectsOutOfScopeCommands()
    {
        var config = SimulationConfig.CreateFallback();
        var state = WorldState.CreateNew("Onboarding Lock");
        state.Progression = ProgressionState.CreateForPopulation(config.Progression, 5000, treatRewardsAsAlreadyGranted: true);
        state.DemoRun.TutorialEnabled = true;
        state.DemoRun.OnboardingCompleted = false;
        state.DemoRun.OnboardingCompletedSteps = 0;
        state.DemoRun.SoftInputLock = true;
        state.DemoRun.Normalize();

        var commands = new CommandBuffer();
        commands.Enqueue(new GameCommand
        {
            Type = GameCommandType.UpdateBudgetPolicy,
            BudgetPolicy = new BudgetPolicyCommandData
            {
                ResidentialTaxRate = 0.12m
            }
        });

        var report = SimulationEngine.SimulationStep(state, commands, 0.5f, config);

        Assert.That(report.RejectedCommandCount, Is.EqualTo(1));
        Assert.That(report.CommandResults[0].RejectionReason, Is.EqualTo(CommandRejectionReason.ProgressionLocked));
        StringAssert.Contains("Tutorial", report.CommandResults[0].Message);
    }

    [Test]
    public void SimulationStep_UpdatesOnboardingDescriptor_AfterRoadPlacement()
    {
        var config = SimulationConfig.CreateFallback();
        var state = WorldState.CreateNew("Onboarding Descriptor");
        var commands = new CommandBuffer();
        commands.Enqueue(new GameCommand
        {
            Type = GameCommandType.BuildRoad,
            BuildRoad = new BuildRoadCommandData
            {
                Start = new Int2(0, 0),
                End = new Int2(2, 0)
            }
        });

        SimulationEngine.SimulationStep(state, commands, 0.5f, config);

        Assert.That(state.DemoRun.OnboardingCompletedSteps, Is.GreaterThanOrEqualTo(1));
        Assert.That(state.DemoRun.OnboardingStepId, Is.EqualTo("onb-02-zone"));
        Assert.That(state.DemoRun.OnboardingStepTitle, Is.Not.Empty);
    }

    [Test]
    public void SimulationStep_SpawnsDeterministicEvent_WhenStateMatches()
    {
        var config = SimulationConfig.CreateFallback();
        var stateA = WorldState.CreateNew("Event Determinism A");
        var stateB = WorldState.CreateNew("Event Determinism B");
        stateA.CityId = "deterministic-city";
        stateB.CityId = "deterministic-city";
        BootstrapPopulationForEvents(stateA, 420);
        BootstrapPopulationForEvents(stateB, 420);

        for (var index = 0; index < 8; index++)
        {
            SimulationEngine.SimulationStep(stateA, new CommandBuffer(), 1f, config);
            SimulationEngine.SimulationStep(stateB, new CommandBuffer(), 1f, config);
        }

        Assert.That(stateA.RunState.ActiveEvent, Is.Not.Null);
        Assert.That(stateB.RunState.ActiveEvent, Is.Not.Null);
        Assert.That(stateA.RunState.ActiveEvent!.EventId, Is.EqualTo(stateB.RunState.ActiveEvent!.EventId));
    }

    [Test]
    public void SimulationStep_ResolveEventChoice_AppliesEffectAndTimedModifier()
    {
        var config = SimulationConfig.CreateFallback();
        var state = WorldState.CreateNew("Event Choice");
        var commandBuffer = new CommandBuffer();
        BootstrapPopulationForEvents(state, 420);

        for (var index = 0; index < 8 && state.RunState.ActiveEvent is null; index++)
        {
            SimulationEngine.SimulationStep(state, new CommandBuffer(), 1f, config);
        }

        Assert.That(state.RunState.ActiveEvent, Is.Not.Null);
        var activeEvent = state.RunState.ActiveEvent!;
        var choice = activeEvent.Choices[0];
        var cashBefore = state.Budget.Cash;

        commandBuffer.Enqueue(new GameCommand
        {
            Type = GameCommandType.ResolveEventChoice,
            ResolveEventChoice = new ResolveEventChoiceCommandData
            {
                EventId = activeEvent.EventId,
                ChoiceId = choice.ChoiceId
            }
        });

        var report = SimulationEngine.SimulationStep(state, commandBuffer, 0.5f, config);

        Assert.That(report.AppliedCommandCount, Is.EqualTo(1));
        Assert.That(state.RunState.ActiveEvent, Is.Null);
        Assert.That(state.RunState.EventHistory.Count, Is.GreaterThan(0));
        Assert.That(state.RunState.ActiveModifiers.Count, Is.GreaterThan(0));
        Assert.That(state.Budget.Cash, Is.Not.EqualTo(cashBefore));
    }

    [Test]
    public void SimulationStep_EventCooldownAndSingleActiveEvent_AreEnforced()
    {
        var config = SimulationConfig.CreateFallback();
        var state = WorldState.CreateNew("Event Cooldown");
        BootstrapPopulationForEvents(state, 420);

        for (var index = 0; index < 8 && state.RunState.ActiveEvent is null; index++)
        {
            SimulationEngine.SimulationStep(state, new CommandBuffer(), 1f, config);
        }

        Assert.That(state.RunState.ActiveEvent, Is.Not.Null);
        var firstEventId = state.RunState.ActiveEvent!.EventId;

        for (var index = 0; index < 12; index++)
        {
            SimulationEngine.SimulationStep(state, new CommandBuffer(), 1f, config);
        }

        Assert.That(state.RunState.ActiveEvent, Is.Not.Null);
        Assert.That(state.RunState.ActiveEvent!.EventId, Is.EqualTo(firstEventId), "Only one event can remain active at a time.");

        var resolveBuffer = new CommandBuffer();
        resolveBuffer.Enqueue(new GameCommand
        {
            Type = GameCommandType.ResolveEventChoice,
            ResolveEventChoice = new ResolveEventChoiceCommandData
            {
                EventId = firstEventId,
                ChoiceId = state.RunState.ActiveEvent.Choices[0].ChoiceId
            }
        });
        SimulationEngine.SimulationStep(state, resolveBuffer, 0.25f, config);

        for (var index = 0; index < 16 && state.RunState.ActiveEvent is null; index++)
        {
            SimulationEngine.SimulationStep(state, new CommandBuffer(), 1f, config);
        }

        Assert.That(state.RunState.ActiveEvent, Is.Not.Null);
        Assert.That(state.RunState.ActiveEvent!.EventId, Is.Not.EqualTo(firstEventId), "Cooldown should prevent immediate same-event recurrence.");
    }

    [Test]
    public void SimulationStep_TransitionsActs_ByPopulationTargets()
    {
        var config = SimulationConfig.CreateFallback();
        var state = WorldState.CreateNew("Act Transition");

        SimulationEngine.SimulationStep(state, new CommandBuffer(), 0.5f, config);
        Assert.That(state.RunState.CurrentActId, Is.EqualTo("act1"));

        BootstrapPopulationForEvents(state, 420);
        SimulationEngine.SimulationStep(state, new CommandBuffer(), 0.5f, config);
        Assert.That(state.RunState.CurrentActId, Is.EqualTo("act2"));

        state.Buildings[0].Residents = 1100;
        SimulationEngine.SimulationStep(state, new CommandBuffer(), 0.5f, config);
        Assert.That(state.RunState.CurrentActId, Is.EqualTo("act3"));
    }

    [Test]
    public void SimulationStep_TriggersEconomicGameOver_AndLocksBuildCommands()
    {
        var config = SimulationConfig.CreateFallback();
        var state = WorldState.CreateNew("Economic Collapse");
        state.Progression.BailoutCount = config.Progression.Bailout.MaxBailouts;
        state.Budget.Cash = -22000m;

        for (var index = 0; index < 25; index++)
        {
            SimulationEngine.SimulationStep(state, new CommandBuffer(), 1f, config);
        }

        Assert.That(state.RunState.IsGameOver, Is.True);

        var commandBuffer = new CommandBuffer();
        commandBuffer.Enqueue(new GameCommand
        {
            Type = GameCommandType.BuildRoad,
            BuildRoad = new BuildRoadCommandData
            {
                Start = new Int2(0, 0),
                End = new Int2(2, 0)
            }
        });

        var report = SimulationEngine.SimulationStep(state, commandBuffer, 0.5f, config);
        Assert.That(report.RejectedCommandCount, Is.EqualTo(1));
        Assert.That(report.CommandResults[0].RejectionReason, Is.EqualTo(CommandRejectionReason.GameOverLocked));
    }

    [Test]
    public void SimulationStep_ComputesDistrictVitality_AndPressures()
    {
        var config = SimulationConfig.CreateFallback();
        var state = WorldState.CreateNew("Vitality Metrics");
        var commands = CreateResidentialBootstrapCommands();

        for (var index = 0; index < 8; index++)
        {
            SimulationEngine.SimulationStep(state, commands, 0.5f, config);
        }

        Assert.That(state.Lots.Count, Is.GreaterThan(0));
        Assert.That(state.Lots[0].DistrictVitality, Is.GreaterThanOrEqualTo(0f));
        Assert.That(state.Lots[0].DistrictVitality, Is.LessThanOrEqualTo(1f));
        Assert.That(state.DemoRun.AverageDistrictVitality, Is.GreaterThan(0f));
        Assert.That(state.DemoRun.ServicePressure, Is.GreaterThanOrEqualTo(0f));
        Assert.That(state.DemoRun.ServicePressure, Is.LessThanOrEqualTo(1f));
    }

    [Test]
    public void SimulationStep_AppliesDelayedEventConsequences()
    {
        var config = SimulationConfig.CreateFallback();
        var state = WorldState.CreateNew("Delayed Consequences");
        BootstrapPopulationForEvents(state, 420);
        config.Events.SpawnIntervalHours = 1f;
        var firstEvent = config.Events.Events[0];
        firstEvent.MinPopulation = 0;
        firstEvent.MinActIndex = 0;
        firstEvent.Choices[0].Effect.DelayedConsequences = new List<DelayedConsequenceDefinition>
        {
            new()
            {
                Id = "aftershock",
                Label = "Aftershock",
                DelayHours = 1f,
                CashDelta = -750m,
                TimedModifiers = new List<TimedModifierDefinition>()
            }
        };

        for (var index = 0; index < 6 && state.RunState.ActiveEvent is null; index++)
        {
            SimulationEngine.SimulationStep(state, new CommandBuffer(), 1f, config);
        }

        Assert.That(state.RunState.ActiveEvent, Is.Not.Null);
        var activeEvent = state.RunState.ActiveEvent!;
        var startingCash = state.Budget.Cash;
        var resolveBuffer = new CommandBuffer();
        resolveBuffer.Enqueue(new GameCommand
        {
            Type = GameCommandType.ResolveEventChoice,
            ResolveEventChoice = new ResolveEventChoiceCommandData
            {
                EventId = activeEvent.EventId,
                ChoiceId = activeEvent.Choices[0].ChoiceId
            }
        });

        SimulationEngine.SimulationStep(state, resolveBuffer, 0.25f, config);
        Assert.That(state.RunState.PendingConsequences.Count, Is.GreaterThan(0));

        SimulationEngine.SimulationStep(state, new CommandBuffer(), 1.5f, config);
        Assert.That(state.RunState.PendingConsequences.Count, Is.EqualTo(0));
        Assert.That(state.Budget.Cash, Is.LessThan(startingCash));
    }

    [Test]
    public void SimulationStep_TriggersDemoVictory_WhenStabilityTargetsMet()
    {
        var config = SimulationConfig.CreateFallback();
        config.Economy.DemoTargetPopulation = 100;
        config.Economy.VictoryMinimumDistrictVitality = 0.55f;
        config.Economy.VictoryMinimumServiceCoverage = 0.60f;
        config.Economy.VictoryMaximumTrafficCongestion = 0.70f;

        var state = WorldState.CreateNew("Victory Run");
        state.RoadNodes.Add(new RoadNode
        {
            Id = "node-0",
            Position = new Int2(0, 0)
        });
        var lot = new ZoneLot
        {
            Id = "lot-1",
            Cell = new Int2(0, 0),
            ZoneType = ZoneType.Residential
        };
        state.Lots.Add(lot);
        state.Buildings.Add(new BuildingState
        {
            Id = "home-1",
            LotId = lot.Id,
            Cell = lot.Cell,
            ZoneType = ZoneType.Residential,
            Residents = 140
        });
        state.Buildings.Add(new BuildingState
        {
            Id = "svc-ele",
            LotId = "svc-ele-lot",
            Cell = new Int2(0, 1),
            ServiceType = ServiceType.Electricity,
            CoverageRadius = 8f
        });
        state.Buildings.Add(new BuildingState
        {
            Id = "svc-wat",
            LotId = "svc-wat-lot",
            Cell = new Int2(1, 0),
            ServiceType = ServiceType.Water,
            CoverageRadius = 8f
        });
        state.Buildings.Add(new BuildingState
        {
            Id = "svc-sew",
            LotId = "svc-sew-lot",
            Cell = new Int2(-1, 0),
            ServiceType = ServiceType.Sewage,
            CoverageRadius = 8f
        });
        state.Buildings.Add(new BuildingState
        {
            Id = "svc-wst",
            LotId = "svc-wst-lot",
            Cell = new Int2(0, -1),
            ServiceType = ServiceType.Waste,
            CoverageRadius = 8f
        });

        SimulationEngine.SimulationStep(state, new CommandBuffer(), 0.5f, config);

        Assert.That(state.RunState.IsVictory, Is.True);
        Assert.That(state.DemoRun.RunCompleted, Is.True);
        Assert.That(state.DemoRun.Outcome, Is.EqualTo(DemoOutcomeType.Victory));
    }

    private static void BootstrapPopulationForEvents(WorldState state, int residents)
    {
        state.Buildings.Clear();
        state.Lots.Clear();
        var lot = new ZoneLot
        {
            Id = "lot-event",
            Cell = new Int2(0, 0),
            ZoneType = ZoneType.Residential,
            HasRoadAccess = true,
            HasElectricity = true,
            HasWater = true,
            HasSewage = true,
            HasWaste = true,
            LandValue = 0.8f
        };
        var building = new BuildingState
        {
            Id = "building-event",
            LotId = lot.Id,
            Cell = lot.Cell,
            ZoneType = ZoneType.Residential,
            Residents = residents,
            Jobs = 0,
            Level = 3,
            Condition = 1f
        };

        state.Lots.Add(lot);
        state.Buildings.Add(building);
    }

    private static CommandBuffer CreateResidentialBootstrapCommands()
    {
        var commands = new CommandBuffer();

        commands.Enqueue(new GameCommand
        {
            Type = GameCommandType.BuildRoad,
            BuildRoad = new BuildRoadCommandData
            {
                Start = new Int2(0, 0),
                End = new Int2(3, 0)
            }
        });

        commands.Enqueue(new GameCommand
        {
            Type = GameCommandType.PaintZone,
            PaintZone = new ZonePaintCommandData
            {
                ZoneType = ZoneType.Residential,
                Cells = { new Int2(1, 1) }
            }
        });

        commands.Enqueue(new GameCommand
        {
            Type = GameCommandType.PlaceService,
            PlaceService = new PlaceServiceCommandData
            {
                ServiceType = ServiceType.Electricity,
                Cell = new Int2(0, 1)
            }
        });

        commands.Enqueue(new GameCommand
        {
            Type = GameCommandType.PlaceService,
            PlaceService = new PlaceServiceCommandData
            {
                ServiceType = ServiceType.Water,
                Cell = new Int2(1, 0)
            }
        });

        commands.Enqueue(new GameCommand
        {
            Type = GameCommandType.PlaceService,
            PlaceService = new PlaceServiceCommandData
            {
                ServiceType = ServiceType.Sewage,
                Cell = new Int2(2, 0)
            }
        });

        return commands;
    }
}
}
