using FluentAssertions;
using NSubstitute;
using PetClinix.Modules.Billing.Application.UseCases.SyncSubscriptionPlan;
using PetClinix.Modules.Billing.Domain.Entities;
using PetClinix.Modules.Billing.Domain.Enums;
using PetClinix.Modules.Billing.Domain.Repositories;
using Xunit;

namespace PetClinix.UnitTests.Handlers;

public class SyncSubscriptionPlanCommandHandlerTests
{
    private readonly ISubscriptionRepository _subscriptionRepositoryMock;
    private readonly SyncSubscriptionPlanCommandHandler _handler;

    public SyncSubscriptionPlanCommandHandlerTests()
    {
        _subscriptionRepositoryMock = Substitute.For<ISubscriptionRepository>();
        _handler = new SyncSubscriptionPlanCommandHandler(_subscriptionRepositoryMock);
    }

    private static SyncSubscriptionPlanCommand CreateValidCommand() => new(
        "sub_test_456",
        PlanTier.Professional);

    [Fact]
    public async Task Handle_Should_Update_PlanTier_When_Subscription_Exists()
    {
        var command = CreateValidCommand();
        var subscription = Subscription.Create(Guid.NewGuid(), "cus_test_123", command.StripeSubscriptionId, PlanTier.Essential);

        _subscriptionRepositoryMock
            .GetByStripeSubscriptionIdAsync(command.StripeSubscriptionId, Arg.Any<CancellationToken>())
            .Returns(subscription);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        subscription.PlanTier.Should().Be(PlanTier.Professional);
        subscription.UpdatedAtUtc.Should().NotBeNull();
        await _subscriptionRepositoryMock.Received(1)
            .UpdateAsync(subscription, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_Should_Not_Update_When_PlanTier_Is_Same()
    {
        var command = new SyncSubscriptionPlanCommand("sub_test_456", PlanTier.Essential);
        var subscription = Subscription.Create(Guid.NewGuid(), "cus_test_123", command.StripeSubscriptionId, PlanTier.Essential);

        _subscriptionRepositoryMock
            .GetByStripeSubscriptionIdAsync(command.StripeSubscriptionId, Arg.Any<CancellationToken>())
            .Returns(subscription);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        subscription.PlanTier.Should().Be(PlanTier.Essential);
        await _subscriptionRepositoryMock.Received(1)
            .UpdateAsync(subscription, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_Should_Return_Failure_When_Subscription_Not_Found()
    {
        var command = CreateValidCommand();

        _subscriptionRepositoryMock
            .GetByStripeSubscriptionIdAsync(command.StripeSubscriptionId, Arg.Any<CancellationToken>())
            .Returns((Subscription?)null);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("billing.subscription.not_found");
        result.ErrorMessage.Should().Be("Assinatura não encontrada.");
        await _subscriptionRepositoryMock.DidNotReceive()
            .UpdateAsync(Arg.Any<Subscription>(), Arg.Any<CancellationToken>());
    }
}