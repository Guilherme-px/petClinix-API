using PetClinix.BuildingBlocks.Domain;
using PetClinix.Modules.Billing.Domain.Enums;

namespace PetClinix.Modules.Billing.Domain.Entities;

public sealed class Subscription : AggregateRoot
{
    public Guid ClinicId { get; private set; }
    public string StripeCustomerId { get; private set; } = string.Empty;
    public string StripeSubscriptionId { get; private set; } = string.Empty;
    public SubscriptionStatus Status { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? UpdatedAtUtc { get; private set; }
    public PlanTier PlanTier { get; private set; }

    private Subscription() { }

    private Subscription(Guid clinicId, string stripeCustomerId, string stripeSubscriptionId, PlanTier planTier, SubscriptionStatus status)
    {
        Id = Guid.NewGuid();
        ClinicId = clinicId;
        StripeCustomerId = stripeCustomerId;
        StripeSubscriptionId = stripeSubscriptionId;
        PlanTier = planTier;
        Status = status;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public static Subscription Create(Guid clinicId, string stripeCustomerId, string stripeSubscriptionId, PlanTier planTier)
    {
        return new Subscription(clinicId, stripeCustomerId, stripeSubscriptionId, planTier, SubscriptionStatus.Active);
    }

    public void MarkAsCanceled()
    {
        Status = SubscriptionStatus.Canceled;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void ChangePlan(PlanTier planTier)
    {
        PlanTier = planTier;
        UpdatedAtUtc = DateTime.UtcNow;
    }
}