using Microsoft.Extensions.Configuration;
using PetClinix.Modules.Billing.Application.Contracts;
using PetClinix.Modules.Billing.Domain.Enums;

namespace PetClinix.Modules.Billing.Infrastructure.Services;

public class StripePlanCatalog : IStripePlanCatalog
{
    private readonly IReadOnlyDictionary<string, PlanTier> _planTierByPriceId;

    public StripePlanCatalog(IConfiguration configuration)
    {
        var mappings = new Dictionary<string, PlanTier>(StringComparer.Ordinal);

        foreach (var tierSection in configuration.GetSection("Stripe:Plans").GetChildren())
        {
            if (!Enum.TryParse<PlanTier>(tierSection.Key, ignoreCase: true, out var planTier))
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(tierSection.Value))
            {
                AddMapping(mappings, tierSection.Value.Trim(), planTier);
            }

            foreach (var priceSection in tierSection.GetChildren())
            {
                if (!string.IsNullOrWhiteSpace(priceSection.Value))
                {
                    AddMapping(mappings, priceSection.Value.Trim(), planTier);
                }
            }
        }

        _planTierByPriceId = mappings;
    }

    public bool TryGetPlanTier(string? priceId, out PlanTier planTier)
    {
        if (!string.IsNullOrEmpty(priceId) && _planTierByPriceId.TryGetValue(priceId, out planTier))
        {
            return true;
        }

        planTier = default;
        return false;
    }

    private static void AddMapping(Dictionary<string, PlanTier> mappings, string priceId, PlanTier planTier)
    {
        if (mappings.TryGetValue(priceId, out var existingTier) && existingTier != planTier)
        {
            throw new InvalidOperationException(
                $"O preço '{priceId}' está mapeado para os planos '{existingTier}' e '{planTier}' em Stripe:Plans. Cada preço pode pertencer a apenas um plano.");
        }

        mappings[priceId] = planTier;
    }
}