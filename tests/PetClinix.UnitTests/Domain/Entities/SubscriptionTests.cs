using FluentAssertions;
using PetClinix.Modules.Billing.Domain.Entities;
using PetClinix.Modules.Billing.Domain.Enums;
using System;
using Xunit;

namespace PetClinix.UnitTests.Domain.Entities;

public class SubscriptionTests
{
    [Fact]
    public void Create_Should_Set_Properties_And_Default_Status_To_Active()
    {
        var clinicId = Guid.NewGuid();
        var stripeCustomerId = "cus_123";
        var stripeSubscriptionId = "sub_456";

        var subscription = Subscription.Create(clinicId, stripeCustomerId, stripeSubscriptionId, PlanTier.Essential);

        subscription.Id.Should().NotBeEmpty();
        subscription.ClinicId.Should().Be(clinicId);
        subscription.StripeCustomerId.Should().Be(stripeCustomerId);
        subscription.StripeSubscriptionId.Should().Be(stripeSubscriptionId);
        subscription.Status.Should().Be(SubscriptionStatus.Active);
        subscription.CreatedAtUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        subscription.UpdatedAtUtc.Should().BeNull();
    }

    [Fact]
    public void MarkAsCanceled_Should_Set_Status_To_Canceled_And_UpdateDate()
    {
        var subscription = Subscription.Create(Guid.NewGuid(), "cus_123", "sub_456", PlanTier.Essential);

        subscription.MarkAsCanceled();

        subscription.Status.Should().Be(SubscriptionStatus.Canceled);
        subscription.UpdatedAtUtc.Should().NotBeNull();
        subscription.UpdatedAtUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }
}