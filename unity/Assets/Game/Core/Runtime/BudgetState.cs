namespace PampaSkylines.Core
{
public sealed class BudgetState
{
    public decimal Cash { get; set; } = 85000m;

    public decimal TaxRateResidential { get; set; } = 0.11m;

    public decimal TaxRateCommercial { get; set; } = 0.10m;

    public decimal TaxRateIndustrial { get; set; } = 0.09m;

    public decimal TaxRateOffice { get; set; } = 0.11m;

    public decimal LoanBalance { get; set; }

    public decimal DailyServiceCost { get; set; }

    public decimal DailyRoadMaintenanceCost { get; set; }

    public decimal DailyConstructionCost { get; set; }

    public decimal DailyIncome { get; set; }

    public decimal LastDailyNet { get; set; }
}
}
