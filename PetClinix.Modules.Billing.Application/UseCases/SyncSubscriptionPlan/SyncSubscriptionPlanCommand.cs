using PetClinix.BuildingBlocks.Application;
using PetClinix.Modules.Billing.Domain.Enums;

namespace PetClinix.Modules.Billing.Application.UseCases.SyncSubscriptionPlan;

public sealed record SyncSubscriptionPlanCommand(string StripeSubscriptionId, PlanTier PlanTier) : ICommand<Result>;