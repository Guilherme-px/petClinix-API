using PetClinix.Modules.Billing.Domain.Enums;

namespace PetClinix.Modules.Billing.Application.Contracts;

public interface IStripePlanCatalog
{
    bool TryGetPlanTier(string? priceId, out PlanTier planTier);
}