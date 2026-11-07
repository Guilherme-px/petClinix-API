using FluentAssertions;
using NSubstitute;
using PetClinix.BuildingBlocks.Application;
using PetClinix.Modules.Identity.Application.Contracts;
using PetClinix.Modules.Identity.Application.UseCases.RegisterStaff;
using PetClinix.Modules.Identity.Domain.Entities;
using PetClinix.Modules.Identity.Domain.Enums;
using PetClinix.Modules.Identity.Domain.Repositories;
using PetClinix.Modules.Identity.Domain.ValueObjects;
using System;
using Xunit;

namespace PetClinix.UnitTests.Handlers;

public class RegisterStaffCommandHandlerTests
{
    private readonly IUserRepository _userRepositoryMock;
    private readonly IUnitOfWork _unitOfWorkMock;
    private readonly ISubscriptionStatusService _subscriptionStatusServiceMock;
    private readonly IEmailService _emailServiceMock;
    private readonly RegisterStaffCommandHandler _handler;

    public RegisterStaffCommandHandlerTests()
    {
        _userRepositoryMock = Substitute.For<IUserRepository>();
        _unitOfWorkMock = Substitute.For<IUnitOfWork>();
        _subscriptionStatusServiceMock = Substitute.For<ISubscriptionStatusService>();
        _emailServiceMock = Substitute.For<IEmailService>();

        _handler = new RegisterStaffCommandHandler(
            _userRepositoryMock,
            _unitOfWorkMock,
            _subscriptionStatusServiceMock,
            _emailServiceMock);
    }

    private static RegisterStaffCommand CreateValidCommand(UserRole role = UserRole.Veterinarian) => new(
        Guid.NewGuid(),
        Guid.NewGuid(),
        "Dr. Dolittle",
        "dolittle@teste.com",
        "12345678900",
        "11999990000",
        new DateOnly(1980, 10, 20),
        role
    );

    [Fact]
    public async Task Handle_Should_ReturnFailure_When_Clinic_Has_No_Active_Subscription()
    {
        var command = CreateValidCommand();

        _subscriptionStatusServiceMock.GetStaffLimitAsync(command.ClinicId, Arg.Any<CancellationToken>()).Returns(0);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("identity.subscription_inactive");

        await _userRepositoryMock.DidNotReceive().CountActiveVeterinariansAsync(command.ClinicId, Arg.Any<CancellationToken>());
        await _userRepositoryMock.DidNotReceive().AddAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
        await _unitOfWorkMock.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        await _emailServiceMock.DidNotReceive().SendWelcomeEmailAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_Should_ReturnFailure_When_Veterinarian_Limit_Is_Reached()
    {
        var command = CreateValidCommand();

        _subscriptionStatusServiceMock.GetStaffLimitAsync(command.ClinicId, Arg.Any<CancellationToken>()).Returns(2);
        _userRepositoryMock.CountActiveVeterinariansAsync(command.ClinicId, Arg.Any<CancellationToken>()).Returns(2);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("identity.staff_limit_reached");

        await _userRepositoryMock.DidNotReceive().AddAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
        await _unitOfWorkMock.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        await _emailServiceMock.DidNotReceive().SendWelcomeEmailAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_Should_ReturnSuccess_When_Active_Veterinarians_Are_Below_Limit()
    {
        var command = CreateValidCommand();

        _subscriptionStatusServiceMock.GetStaffLimitAsync(command.ClinicId, Arg.Any<CancellationToken>()).Returns(2);
        _userRepositoryMock.CountActiveVeterinariansAsync(command.ClinicId, Arg.Any<CancellationToken>()).Returns(1);
        _userRepositoryMock.ExistsByEmailAsync(Arg.Any<Email>(), Arg.Any<CancellationToken>()).Returns(false);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        await _userRepositoryMock.Received(1).AddAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
        await _unitOfWorkMock.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await _emailServiceMock.Received(1).SendWelcomeEmailAsync(command.Email, command.Name, Arg.Is<string>(t => !string.IsNullOrWhiteSpace(t)), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_Should_ReturnSuccess_When_Plan_Is_Premium_Without_Limit()
    {
        var command = CreateValidCommand();

        _subscriptionStatusServiceMock.GetStaffLimitAsync(command.ClinicId, Arg.Any<CancellationToken>()).Returns(int.MaxValue);
        _userRepositoryMock.ExistsByEmailAsync(Arg.Any<Email>(), Arg.Any<CancellationToken>()).Returns(false);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        await _userRepositoryMock.DidNotReceive().CountActiveVeterinariansAsync(command.ClinicId, Arg.Any<CancellationToken>());
        await _userRepositoryMock.Received(1).AddAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
        await _unitOfWorkMock.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await _emailServiceMock.Received(1).SendWelcomeEmailAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_Should_ReturnFailure_When_Email_Already_Exists()
    {
        var command = CreateValidCommand();

        _subscriptionStatusServiceMock.GetStaffLimitAsync(command.ClinicId, Arg.Any<CancellationToken>()).Returns(2);
        _userRepositoryMock.CountActiveVeterinariansAsync(command.ClinicId, Arg.Any<CancellationToken>()).Returns(1);
        _userRepositoryMock.ExistsByEmailAsync(Arg.Any<Email>(), Arg.Any<CancellationToken>()).Returns(true);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("identity.user.email_already_exists");

        await _userRepositoryMock.DidNotReceive().AddAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
        await _unitOfWorkMock.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_Should_ReturnSuccess_When_Role_Is_Receptionist_Even_Without_Vet_Slots()
    {
        var command = CreateValidCommand(UserRole.Receptionist);

        _userRepositoryMock.ExistsByEmailAsync(Arg.Any<Email>(), Arg.Any<CancellationToken>()).Returns(false);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        await _subscriptionStatusServiceMock.DidNotReceive().GetStaffLimitAsync(command.ClinicId, Arg.Any<CancellationToken>());
        await _userRepositoryMock.DidNotReceive().CountActiveVeterinariansAsync(command.ClinicId, Arg.Any<CancellationToken>());
        await _userRepositoryMock.Received(1).AddAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
        await _unitOfWorkMock.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await _emailServiceMock.Received(1).SendWelcomeEmailAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}