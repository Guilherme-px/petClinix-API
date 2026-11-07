using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PetClinix.Modules.Billing.Domain.Entities;
using PetClinix.Modules.Billing.Domain.Repositories;
using PetClinix.Modules.Billing.Domain.Enums;
using PetClinix.Modules.Billing.Infrastructure.Persistence;
using Xunit;

namespace PetClinix.IntegrationTests.Repositories;

public class SubscriptionRepositoryIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public SubscriptionRepositoryIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task AddAsync_Should_Save_Subscription_In_Database()
    {
        using var scope = _factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<ISubscriptionRepository>();
        var context = scope.ServiceProvider.GetRequiredService<BillingDbContext>();

        var clinicId = Guid.NewGuid();
        var subscription = Subscription.Create(clinicId, "cus_integration_1", "sub_integration_1", PlanTier.Essential);

        await repository.AddAsync(subscription);

        var savedSubscription = await context.Subscriptions.FirstOrDefaultAsync(s => s.ClinicId == clinicId);

        savedSubscription.Should().NotBeNull();
        savedSubscription!.StripeCustomerId.Should().Be("cus_integration_1");
        savedSubscription.StripeSubscriptionId.Should().Be("sub_integration_1");
    }

    [Fact]
    public async Task GetByClinicIdAsync_Should_Return_Saved_Subscription()
    {
        using var scope = _factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<ISubscriptionRepository>();
        var context = scope.ServiceProvider.GetRequiredService<BillingDbContext>();

        var clinicId = Guid.NewGuid();
        var subscription = Subscription.Create(clinicId, "cus_integration_2", "sub_integration_2", PlanTier.Essential);

        await context.Subscriptions.AddAsync(subscription);
        await context.SaveChangesAsync();

        var result = await repository.GetByClinicIdAsync(clinicId);

        result.Should().NotBeNull();
        result!.StripeCustomerId.Should().Be("cus_integration_2");
        result.StripeSubscriptionId.Should().Be("sub_integration_2");
    }
}