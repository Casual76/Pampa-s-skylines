#nullable enable

namespace PampaSkylines.Commands
{
public sealed class BudgetPolicyCommandData
{
    public decimal? ResidentialTaxRate { get; set; }

    public decimal? CommercialTaxRate { get; set; }

    public decimal? IndustrialTaxRate { get; set; }

    public decimal? OfficeTaxRate { get; set; }
}
}
