using PetClinix.BuildingBlocks.Application;
using PetClinix.Modules.Billing.Domain.Repositories;

namespace PetClinix.Modules.Billing.Application.UseCases.SyncSubscriptionPlan;

public sealed class SyncSubscriptionPlanCommandHandler : ICommandHandler<SyncSubscriptionPlanCommand, Result>
{
    private readonly ISubscriptionRepository _subscriptionRepository;

    public SyncSubscriptionPlanCommandHandler(ISubscriptionRepository subscriptionRepository)
    {
        _subscriptionRepository = subscriptionRepository;
    }

    public async Task<Result> Handle(SyncSubscriptionPlanCommand command, CancellationToken cancellationToken)
    {
        var subscription = await _subscriptionRepository.GetByStripeSubscriptionIdAsync(command.StripeSubscriptionId, cancellationToken);

        if (subscription == null)
        {
            return Result.Failure("billing.subscription.not_found", "Assinatura não encontrada.");
        }

        subscription.ChangePlan(command.PlanTier);
        await _subscriptionRepository.UpdateAsync(subscription, cancellationToken);

        return Result.Success();
    }
}