using Microsoft.EntityFrameworkCore;
using PetClinix.Modules.Billing.Domain.Enums;
using PetClinix.Modules.Billing.Infrastructure.Persistence;
using PetClinix.Modules.Identity.Application.Contracts;

namespace PetClinix.Modules.Billing.Infrastructure.Services;

public class SubscriptionStatusService : ISubscriptionStatusService
{
    private readonly BillingDbContext _context;

    public SubscriptionStatusService(BillingDbContext context)
    {
        _context = context;
    }

    public async Task<bool> IsClinicActiveAsync(Guid clinicId, CancellationToken cancellationToken = default)
    {
        var subscription = await _context.Subscriptions
            .FirstOrDefaultAsync(s => s.ClinicId == clinicId, cancellationToken);

        if (subscription == null) return false;

        return subscription.Status == SubscriptionStatus.Active;
    }

    public async Task<int> GetStaffLimitAsync(Guid clinicId, CancellationToken cancellationToken = default)
    {
        var subscription = await _context.Subscriptions
            .FirstOrDefaultAsync(s => s.ClinicId == clinicId, cancellationToken);

        if (subscription == null || subscription.Status != SubscriptionStatus.Active)
            return 0;

        return subscription.PlanTier switch
        {
            PlanTier.Essential => 2,
            PlanTier.Professional => 5,
            PlanTier.Premium => int.MaxValue,
            _ => 0
        };
    }
}