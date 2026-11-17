using FluentAssertions;
using NSubstitute;
using PetClinix.Modules.Identity.Application.Contracts;
using PetClinix.Modules.Identity.Application.UseCases.GetStaffLimit;
using PetClinix.Modules.Identity.Domain.Repositories;
using System;
using Xunit;

namespace PetClinix.UnitTests.Handlers;

public class GetStaffLimitQueryHandlerTests
{
    private readonly IUserRepository _userRepositoryMock;
    private readonly ISubscriptionStatusService _subscriptionStatusServiceMock;
    private readonly GetStaffLimitQueryHandler _handler;

    public GetStaffLimitQueryHandlerTests()
    {
        _userRepositoryMock = Substitute.For<IUserRepository>();
        _subscriptionStatusServiceMock = Substitute.For<ISubscriptionStatusService>();
        _handler = new GetStaffLimitQueryHandler(_userRepositoryMock, _subscriptionStatusServiceMock);
    }

    [Fact]
    public async Task Handle_Should_ReturnUsedAndLimit_When_Plan_Has_Limit()
    {
        var query = new GetStaffLimitQuery(Guid.NewGuid());

        _userRepositoryMock.CountActiveVeterinariansAsync(query.ClinicId, Arg.Any<CancellationToken>()).Returns(3);
        _subscriptionStatusServiceMock.GetStaffLimitAsync(query.ClinicId, Arg.Any<CancellationToken>()).Returns(5);

        var result = await _handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Used.Should().Be(3);
        result.Value!.Limit.Should().Be(5);
    }

    [Fact]
    public async Task Handle_Should_ReturnNullLimit_When_Plan_Is_Unlimited()
    {
        var query = new GetStaffLimitQuery(Guid.NewGuid());

        _userRepositoryMock.CountActiveVeterinariansAsync(query.ClinicId, Arg.Any<CancellationToken>()).Returns(7);
        _subscriptionStatusServiceMock.GetStaffLimitAsync(query.ClinicId, Arg.Any<CancellationToken>()).Returns(int.MaxValue);

        var result = await _handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Used.Should().Be(7);
        result.Value!.Limit.Should().BeNull();
    }

    [Fact]
    public async Task Handle_Should_ReturnNullLimit_When_Subscription_Is_Inactive()
    {
        var query = new GetStaffLimitQuery(Guid.NewGuid());

        _userRepositoryMock.CountActiveVeterinariansAsync(query.ClinicId, Arg.Any<CancellationToken>()).Returns(2);
        _subscriptionStatusServiceMock.GetStaffLimitAsync(query.ClinicId, Arg.Any<CancellationToken>()).Returns(0);

        var result = await _handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Used.Should().Be(2);
        result.Value!.Limit.Should().BeNull();
    }
}