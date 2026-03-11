#nullable enable

namespace PampaSkylines.Core
{
using System;
using System.Collections.Generic;
using System.Linq;

public sealed class EventCatalog
{
    public float SpawnIntervalHours { get; set; } = 4f;

    public List<CityActDefinition> Acts { get; set; } = new();

    public List<CityEventDefinition> Events { get; set; } = new();

    public int ResolveActIndexForPopulation(int population)
    {
        if (Acts.Count == 0)
        {
            return 0;
        }

        var clampedPopulation = Math.Max(0, population);
        var index = 0;
        for (var candidate = 0; candidate < Acts.Count; candidate++)
        {
            var act = Acts[candidate];
            var withinMin = clampedPopulation >= act.MinPopulation;
            var withinMax = act.MaxPopulation <= 0 || clampedPopulation <= act.MaxPopulation;
            if (withinMin && withinMax)
            {
                index = candidate;
            }
        }

        return Math.Clamp(index, 0, Acts.Count - 1);
    }

    public CityActDefinition ResolveActForPopulation(int population)
    {
        if (Acts.Count == 0)
        {
            return CityActDefinition.CreateDefault();
        }

        return Acts[Math.Clamp(ResolveActIndexForPopulation(population), 0, Acts.Count - 1)];
    }

    public CityEventDefinition? FindEvent(string? eventId)
    {
        if (string.IsNullOrWhiteSpace(eventId))
        {
            return null;
        }

        return Events.FirstOrDefault(candidate => string.Equals(candidate.Id, eventId, StringComparison.Ordinal));
    }

    public static EventCatalog CreateDefault()
    {
        return new EventCatalog
        {
            SpawnIntervalHours = 4f,
            Acts = new List<CityActDefinition>
            {
                new()
                {
                    Id = "act1",
                    DisplayName = "Fondazione",
                    MinPopulation = 0,
                    MaxPopulation = 319,
                    ObjectivePopulationTarget = 320,
                    ObjectiveDescription = "Raggiungi 320 abitanti per avviare l'espansione."
                },
                new()
                {
                    Id = "act2",
                    DisplayName = "Espansione",
                    MinPopulation = 320,
                    MaxPopulation = 949,
                    ObjectivePopulationTarget = 950,
                    ObjectiveDescription = "Consolida servizi e lavoro fino a 950 abitanti."
                },
                new()
                {
                    Id = "act3",
                    DisplayName = "Pressione Metropolitana",
                    MinPopulation = 950,
                    MaxPopulation = 0,
                    ObjectivePopulationTarget = 1250,
                    ObjectiveDescription = "Mantieni la citta stabile durante la crescita metropolitana."
                }
            },
            Events = new List<CityEventDefinition>
            {
                new()
                {
                    Id = "power-surge",
                    Title = "Sbalzo di rete elettrica",
                    Description = "Un trasformatore e al limite. Intervenire ora evita disservizi.",
                    Weight = 5,
                    MinPopulation = 30,
                    MinActIndex = 0,
                    CooldownHours = 20f,
                    Choices = new List<CityEventChoiceDefinition>
                    {
                        new()
                        {
                            Id = "repair-now",
                            Label = "Ripara subito",
                            Description = "Intervento tecnico immediato.",
                            Effect = new EventChoiceEffectDefinition
                            {
                                CashDelta = -1800m,
                                TimedModifiers = new List<TimedModifierDefinition>
                                {
                                    new()
                                    {
                                        Id = "grid-stable",
                                        Label = "Rete stabilizzata",
                                        DurationHours = 16f,
                                        GrowthMultiplier = 1.06f,
                                        ServiceCostMultiplier = 1.02f
                                    }
                                }
                            }
                        },
                        new()
                        {
                            Id = "defer-repair",
                            Label = "Rimanda i lavori",
                            Description = "Risparmi oggi, rischi rallentamenti.",
                            Effect = new EventChoiceEffectDefinition
                            {
                                CashDelta = 700m,
                                ResidentialDemandDelta = -0.08f,
                                TimedModifiers = new List<TimedModifierDefinition>
                                {
                                    new()
                                    {
                                        Id = "grid-instability",
                                        Label = "Instabilita di rete",
                                        DurationHours = 18f,
                                        GrowthMultiplier = 0.86f,
                                        ServiceCostMultiplier = 1.08f
                                    }
                                }
                            }
                        }
                    }
                },
                new()
                {
                    Id = "street-market",
                    Title = "Mercato civico del weekend",
                    Description = "I commercianti chiedono spazio e supporto logistico.",
                    Weight = 4,
                    MinPopulation = 60,
                    MinActIndex = 0,
                    CooldownHours = 22f,
                    Choices = new List<CityEventChoiceDefinition>
                    {
                        new()
                        {
                            Id = "sponsor-market",
                            Label = "Sponsorizza il mercato",
                            Description = "Aumenti attrattivita commerciale.",
                            Effect = new EventChoiceEffectDefinition
                            {
                                CashDelta = -900m,
                                CommercialDemandDelta = 0.12f,
                                TimedModifiers = new List<TimedModifierDefinition>
                                {
                                    new()
                                    {
                                        Id = "market-weekend",
                                        Label = "Afflusso commerciale",
                                        DurationHours = 16f,
                                        CommercialDemandMultiplier = 1.18f,
                                        CommuteMinutesMultiplier = 1.08f
                                    }
                                }
                            }
                        },
                        new()
                        {
                            Id = "tax-market",
                            Label = "Concedi permessi a pagamento",
                            Description = "Incassi immediati ma meno entusiasmo.",
                            Effect = new EventChoiceEffectDefinition
                            {
                                CashDelta = 1400m,
                                CommercialDemandDelta = -0.05f
                            }
                        }
                    }
                },
                new()
                {
                    Id = "waste-strike",
                    Title = "Sciopero raccolta rifiuti",
                    Description = "Il servizio rifiuti rischia blocchi su piu quartieri.",
                    Weight = 5,
                    MinPopulation = 160,
                    MinActIndex = 1,
                    CooldownHours = 26f,
                    Choices = new List<CityEventChoiceDefinition>
                    {
                        new()
                        {
                            Id = "bonus-shifts",
                            Label = "Straordinari immediati",
                            Description = "Servizio ripristinato ma con costo extra.",
                            Effect = new EventChoiceEffectDefinition
                            {
                                CashDelta = -2400m,
                                TimedModifiers = new List<TimedModifierDefinition>
                                {
                                    new()
                                    {
                                        Id = "waste-overtime",
                                        Label = "Straordinari servizi",
                                        DurationHours = 24f,
                                        ServiceCostMultiplier = 1.15f
                                    }
                                }
                            }
                        },
                        new()
                        {
                            Id = "wait-it-out",
                            Label = "Attendi trattativa",
                            Description = "Riduci la spesa ma subisci malus temporanei.",
                            Effect = new EventChoiceEffectDefinition
                            {
                                ResidentialDemandDelta = -0.10f,
                                LandValueDelta = -0.08f,
                                TimedModifiers = new List<TimedModifierDefinition>
                                {
                                    new()
                                    {
                                        Id = "waste-delay",
                                        Label = "Disagio rifiuti",
                                        DurationHours = 20f,
                                        GrowthMultiplier = 0.84f
                                    }
                                }
                            }
                        }
                    }
                },
                new()
                {
                    Id = "flood-alert",
                    Title = "Allerta idrica improvvisa",
                    Description = "Piogge intense mettono in stress rete acqua e fogne.",
                    Weight = 4,
                    MinPopulation = 260,
                    MinActIndex = 1,
                    CooldownHours = 24f,
                    Choices = new List<CityEventChoiceDefinition>
                    {
                        new()
                        {
                            Id = "emergency-crews",
                            Label = "Squadre d'emergenza",
                            Description = "Limiti i danni con investimento immediato.",
                            Effect = new EventChoiceEffectDefinition
                            {
                                CashDelta = -2100m,
                                TimedModifiers = new List<TimedModifierDefinition>
                                {
                                    new()
                                    {
                                        Id = "flood-controlled",
                                        Label = "Rete sotto controllo",
                                        DurationHours = 16f,
                                        GrowthMultiplier = 1.03f
                                    }
                                }
                            }
                        },
                        new()
                        {
                            Id = "minimal-response",
                            Label = "Risposta minima",
                            Description = "Tagli i costi ma il disagio rallenta la citta.",
                            Effect = new EventChoiceEffectDefinition
                            {
                                CashDelta = 500m,
                                UtilityCoverageDelta = -0.08f,
                                TimedModifiers = new List<TimedModifierDefinition>
                                {
                                    new()
                                    {
                                        Id = "flood-disruption",
                                        Label = "Disservizi idrici",
                                        DurationHours = 18f,
                                        GrowthMultiplier = 0.82f
                                    }
                                }
                            }
                        }
                    }
                },
                new()
                {
                    Id = "tax-protest",
                    Title = "Proteste fiscali",
                    Description = "Comitati cittadini contestano il peso fiscale attuale.",
                    Weight = 4,
                    MinPopulation = 340,
                    MinActIndex = 1,
                    CooldownHours = 20f,
                    Choices = new List<CityEventChoiceDefinition>
                    {
                        new()
                        {
                            Id = "cut-taxes",
                            Label = "Riduci la pressione",
                            Description = "Perdi cassa ma rilanci fiducia e crescita.",
                            Effect = new EventChoiceEffectDefinition
                            {
                                CashDelta = -1000m,
                                ResidentialDemandDelta = 0.08f,
                                CommercialDemandDelta = 0.05f,
                                TimedModifiers = new List<TimedModifierDefinition>
                                {
                                    new()
                                    {
                                        Id = "tax-relief",
                                        Label = "Sollievo fiscale",
                                        DurationHours = 20f,
                                        GrowthMultiplier = 1.08f,
                                        TaxIncomeMultiplier = 0.94f
                                    }
                                }
                            }
                        },
                        new()
                        {
                            Id = "hold-line",
                            Label = "Mantieni linea dura",
                            Description = "Preservi entrate ma aumenti tensione.",
                            Effect = new EventChoiceEffectDefinition
                            {
                                CashDelta = 1200m,
                                ResidentialDemandDelta = -0.06f,
                                OfficeDemandDelta = -0.04f
                            }
                        }
                    }
                },
                new()
                {
                    Id = "startup-district",
                    Title = "Consorzio startup",
                    Description = "Investitori offrono un hub innovazione con cofinanziamento.",
                    Weight = 3,
                    MinPopulation = 980,
                    MinActIndex = 2,
                    CooldownHours = 30f,
                    Choices = new List<CityEventChoiceDefinition>
                    {
                        new()
                        {
                            Id = "fund-hub",
                            Label = "Finanzia il distretto",
                            Description = "Spesa iniziale, forte spinta uffici e occupazione.",
                            Effect = new EventChoiceEffectDefinition
                            {
                                CashDelta = -4200m,
                                OfficeDemandDelta = 0.18f,
                                TimedModifiers = new List<TimedModifierDefinition>
                                {
                                    new()
                                    {
                                        Id = "startup-boom",
                                        Label = "Boom uffici",
                                        DurationHours = 28f,
                                        OfficeDemandMultiplier = 1.22f,
                                        GrowthMultiplier = 1.10f
                                    }
                                }
                            }
                        },
                        new()
                        {
                            Id = "decline-hub",
                            Label = "Rifiuta proposta",
                            Description = "Nessun costo ma opportunita persa.",
                            Effect = new EventChoiceEffectDefinition
                            {
                                CashDelta = 0m,
                                OfficeDemandDelta = -0.06f
                            }
                        }
                    }
                },
                new()
                {
                    Id = "flu-wave",
                    Title = "Picco influenzale",
                    Description = "Aumentano assenze e pressione sui servizi pubblici.",
                    Weight = 4,
                    MinPopulation = 900,
                    MinActIndex = 2,
                    CooldownHours = 24f,
                    Choices = new List<CityEventChoiceDefinition>
                    {
                        new()
                        {
                            Id = "health-campaign",
                            Label = "Campagna sanitaria",
                            Description = "Riduci l'impatto con un piano urgente.",
                            Effect = new EventChoiceEffectDefinition
                            {
                                CashDelta = -2500m,
                                TimedModifiers = new List<TimedModifierDefinition>
                                {
                                    new()
                                    {
                                        Id = "health-response",
                                        Label = "Risposta sanitaria",
                                        DurationHours = 18f,
                                        GrowthMultiplier = 0.98f,
                                        ServiceCostMultiplier = 1.10f
                                    }
                                }
                            }
                        },
                        new()
                        {
                            Id = "limited-response",
                            Label = "Intervento limitato",
                            Description = "Contieni i costi ma rallenti la citta.",
                            Effect = new EventChoiceEffectDefinition
                            {
                                CashDelta = -600m,
                                ResidentialDemandDelta = -0.08f,
                                IndustrialDemandDelta = -0.05f,
                                TimedModifiers = new List<TimedModifierDefinition>
                                {
                                    new()
                                    {
                                        Id = "flu-disruption",
                                        Label = "Assenteismo diffuso",
                                        DurationHours = 18f,
                                        GrowthMultiplier = 0.82f
                                    }
                                }
                            }
                        }
                    }
                },
                new()
                {
                    Id = "mobility-plan",
                    Title = "Piano mobilita urbana",
                    Description = "I tecnici propongono interventi rapidi per fluidificare il traffico.",
                    Weight = 3,
                    MinPopulation = 700,
                    MinActIndex = 2,
                    CooldownHours = 28f,
                    Choices = new List<CityEventChoiceDefinition>
                    {
                        new()
                        {
                            Id = "execute-plan",
                            Label = "Applica il piano",
                            Description = "Migliori il traffico con investimento mirato.",
                            Effect = new EventChoiceEffectDefinition
                            {
                                CashDelta = -3200m,
                                TimedModifiers = new List<TimedModifierDefinition>
                                {
                                    new()
                                    {
                                        Id = "mobility-upgrade",
                                        Label = "Traffico ottimizzato",
                                        DurationHours = 24f,
                                        CommuteMinutesMultiplier = 0.86f,
                                        RoadMaintenanceMultiplier = 1.06f
                                    }
                                }
                            }
                        },
                        new()
                        {
                            Id = "skip-plan",
                            Label = "Posticipa il piano",
                            Description = "Eviti costi oggi ma cresce la congestione.",
                            Effect = new EventChoiceEffectDefinition
                            {
                                CashDelta = 0m,
                                TimedModifiers = new List<TimedModifierDefinition>
                                {
                                    new()
                                    {
                                        Id = "mobility-strain",
                                        Label = "Congestione crescente",
                                        DurationHours = 20f,
                                        CommuteMinutesMultiplier = 1.18f
                                    }
                                }
                            }
                        }
                    }
                }
            }
        };
    }
}

public sealed class CityActDefinition
{
    public string Id { get; set; } = "act1";

    public string DisplayName { get; set; } = "Fondazione";

    public int MinPopulation { get; set; }

    public int MaxPopulation { get; set; } = 319;

    public int ObjectivePopulationTarget { get; set; } = 320;

    public string ObjectiveDescription { get; set; } = "Raggiungi 320 abitanti.";

    public static CityActDefinition CreateDefault()
    {
        return new CityActDefinition
        {
            Id = "act1",
            DisplayName = "Fondazione",
            MinPopulation = 0,
            MaxPopulation = 319,
            ObjectivePopulationTarget = 320,
            ObjectiveDescription = "Raggiungi 320 abitanti."
        };
    }
}

public sealed class CityEventDefinition
{
    public string Id { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public int Weight { get; set; } = 1;

    public int MinPopulation { get; set; }

    public int MaxPopulation { get; set; }

    public int MinActIndex { get; set; }

    public float CooldownHours { get; set; } = 12f;

    public List<CityEventChoiceDefinition> Choices { get; set; } = new();
}

public sealed class CityEventChoiceDefinition
{
    public string Id { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public EventChoiceEffectDefinition Effect { get; set; } = new();
}

public sealed class EventChoiceEffectDefinition
{
    public decimal CashDelta { get; set; }

    public decimal LoanDelta { get; set; }

    public float ResidentialDemandDelta { get; set; }

    public float CommercialDemandDelta { get; set; }

    public float IndustrialDemandDelta { get; set; }

    public float OfficeDemandDelta { get; set; }

    public float LandValueDelta { get; set; }

    public float UtilityCoverageDelta { get; set; }

    public List<TimedModifierDefinition> TimedModifiers { get; set; } = new();

    public List<DelayedConsequenceDefinition> DelayedConsequences { get; set; } = new();
}

public sealed class DelayedConsequenceDefinition
{
    public string Id { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    public float DelayHours { get; set; } = 6f;

    public string FollowUpEventId { get; set; } = string.Empty;

    public decimal CashDelta { get; set; }

    public decimal LoanDelta { get; set; }

    public float ResidentialDemandDelta { get; set; }

    public float CommercialDemandDelta { get; set; }

    public float IndustrialDemandDelta { get; set; }

    public float OfficeDemandDelta { get; set; }

    public float LandValueDelta { get; set; }

    public float UtilityCoverageDelta { get; set; }

    public List<TimedModifierDefinition> TimedModifiers { get; set; } = new();
}

public sealed class TimedModifierDefinition
{
    public string Id { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    public float DurationHours { get; set; } = 12f;

    public float ResidentialDemandMultiplier { get; set; } = 1f;

    public float CommercialDemandMultiplier { get; set; } = 1f;

    public float IndustrialDemandMultiplier { get; set; } = 1f;

    public float OfficeDemandMultiplier { get; set; } = 1f;

    public float GrowthMultiplier { get; set; } = 1f;

    public float ServiceCostMultiplier { get; set; } = 1f;

    public float RoadMaintenanceMultiplier { get; set; } = 1f;

    public float CommuteMinutesMultiplier { get; set; } = 1f;

    public float TaxIncomeMultiplier { get; set; } = 1f;
}
}
