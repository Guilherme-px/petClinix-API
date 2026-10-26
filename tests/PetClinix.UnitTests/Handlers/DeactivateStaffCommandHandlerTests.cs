using FluentAssertions;
using NSubstitute;
using PetClinix.BuildingBlocks.Application;
using PetClinix.Modules.Identity.Application.UseCases.DeactivateStaff;
using PetClinix.Modules.Identity.Domain.Entities;
using PetClinix.Modules.Identity.Domain.Enums;
using PetClinix.Modules.Identity.Domain.Repositories;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace PetClinix.UnitTests.Handlers;

public class DeactivateStaffCommandHandlerTests
{
    private readonly IUserRepository _userRepositoryMock;
    private readonly IUnitOfWork _unitOfWorkMock;
    private readonly DeactivateStaffCommandHandler _handler;
    private readonly IAppointmentDependencyChecker _appointmentDependencyCheckerMock;

    public DeactivateStaffCommandHandlerTests()
    {
        _userRepositoryMock = Substitute.For<IUserRepository>();
        _unitOfWorkMock = Substitute.For<IUnitOfWork>();
        _appointmentDependencyCheckerMock = Substitute.For<IAppointmentDependencyChecker>();
        _handler = new DeactivateStaffCommandHandler(
            _userRepositoryMock,
            _unitOfWorkMock,
            _appointmentDependencyCheckerMock
        );
    }

    private static User CreateValidStaffUser(Guid clinicId)
    {
        return User.CreateStaff(
            clinicId, Guid.NewGuid(), "Dr. Dolittle", "dr@teste.com", "hash", "12345678900", "11999990000",
            new DateOnly(1990, 1, 1), UserRole.Veterinarian
        );
    }

    private static DeactivateStaffCommand CreateValidCommand(Guid clinicId, Guid userId) =>
       new(clinicId, userId);

    [Fact]
    public async Task Handle_Should_ReturnFailure_When_User_Does_Not_Exist()
    {
        var command = new DeactivateStaffCommand(Guid.NewGuid(), Guid.NewGuid());
        _userRepositoryMock.GetByIdAsync(command.UserId, Arg.Any<CancellationToken>()).Returns((User?)null);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("identity.user.not_found");
    }

    [Fact]
    public async Task Handle_Should_ReturnFailure_When_User_Belongs_To_Another_Clinic()
    {
        var myClinicId = Guid.NewGuid();
        var otherClinicId = Guid.NewGuid();
        var user = CreateValidStaffUser(otherClinicId);

        var command = new DeactivateStaffCommand(myClinicId, user.Id);
        _userRepositoryMock.GetByIdAsync(command.UserId, Arg.Any<CancellationToken>()).Returns(user);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("identity.user.not_found");
    }

    [Fact]
    public async Task Handle_Should_ReturnFailure_When_User_Has_Future_Appointments()
    {
        var clinicId = Guid.NewGuid();
        var user = CreateValidStaffUser(clinicId);
        var command = new DeactivateStaffCommand(clinicId, user.Id);

        _userRepositoryMock.GetByIdAsync(command.UserId, Arg.Any<CancellationToken>()).Returns(user);
        _appointmentDependencyCheckerMock.HasFutureAppointmentsForVetAsync(command.ClinicId, user.Id, Arg.Any<CancellationToken>()).Returns(true);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("identity.user.has_future_appointments");
    }

    [Fact]
    public async Task Handle_Should_ReturnSuccess_And_Deactivate_User_When_Valid()
    {
        var clinicId = Guid.NewGuid();
        var user = CreateValidStaffUser(clinicId);
        var command = new DeactivateStaffCommand(clinicId, user.Id);

        _userRepositoryMock.GetByIdAsync(command.UserId, Arg.Any<CancellationToken>()).Returns(user);
        _appointmentDependencyCheckerMock.HasFutureAppointmentsForVetAsync(command.ClinicId, user.Id, Arg.Any<CancellationToken>()).Returns(false);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        user.IsActive.Should().BeFalse();
    }
}