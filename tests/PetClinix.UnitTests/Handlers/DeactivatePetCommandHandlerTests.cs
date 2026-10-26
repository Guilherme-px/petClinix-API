using FluentAssertions;
using NSubstitute;
using PetClinix.BuildingBlocks.Application;
using PetClinix.Modules.Pets.Application.Contracts;
using PetClinix.Modules.Pets.Application.UseCases.DeactivatePet;
using PetClinix.Modules.Pets.Domain.Entities;
using PetClinix.Modules.Pets.Domain.Enums;
using PetClinix.Modules.Pets.Domain.Repositories;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace PetClinix.UnitTests.Handlers;

public class DeactivatePetCommandHandlerTests
{
    private readonly IPetRepository _petRepositoryMock;
    private readonly IPetsUnitOfWork _unitOfWorkMock;
    private readonly DeactivatePetCommandHandler _handler;
    private readonly IAppointmentDependencyChecker _appointmentDependencyCheckerMock;

    public DeactivatePetCommandHandlerTests()
    {
        _petRepositoryMock = Substitute.For<IPetRepository>();
        _unitOfWorkMock = Substitute.For<IPetsUnitOfWork>();
        _appointmentDependencyCheckerMock = Substitute.For<IAppointmentDependencyChecker>();
        _handler = new DeactivatePetCommandHandler(
            _petRepositoryMock,
            _unitOfWorkMock,
            _appointmentDependencyCheckerMock
        );
    }

    private static Pet CreateValidPet(Guid clinicId, Guid tutorId)
    {
        return Pet.Create(
            clinicId, tutorId, Guid.NewGuid(), "Rex", Species.Dog, "Vira Lata",
            new DateOnly(2020, 5, 10), PetSex.Male, 15.5, true, null
        );
    }

    [Fact]
    public async Task Handle_Should_ReturnFailure_When_Pet_Does_Not_Exist()
    {
        var command = new DeactivatePetCommand(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        _petRepositoryMock.GetByIdAsync(command.ClinicId, command.PetId, Arg.Any<CancellationToken>()).Returns((Pet?)null);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("pets.pet.not_found");
    }

    [Fact]
    public async Task Handle_Should_ReturnFailure_When_Pet_From_Another_Clinic_Is_Not_Returned_By_Repository()
    {
        var myClinicId = Guid.NewGuid();
        var myTutorId = Guid.NewGuid();
        var otherClinicId = Guid.NewGuid();
        var otherTutorId = Guid.NewGuid();

        var pet = CreateValidPet(otherClinicId, otherTutorId);

        var command = new DeactivatePetCommand(myClinicId, myTutorId, pet.Id);

        _petRepositoryMock.GetByIdAsync(command.ClinicId, command.PetId, Arg.Any<CancellationToken>()).Returns((Pet?)null);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("pets.pet.not_found");
    }

    [Fact]
    public async Task Handle_Should_ReturnFailure_When_Pet_Belongs_To_Another_Tutor()
    {
        var clinicId = Guid.NewGuid();
        var myTutorId = Guid.NewGuid();
        var otherTutorId = Guid.NewGuid();

        var pet = CreateValidPet(clinicId, otherTutorId);

        var command = new DeactivatePetCommand(clinicId, myTutorId, pet.Id);
        _petRepositoryMock.GetByIdAsync(command.ClinicId, command.PetId, Arg.Any<CancellationToken>()).Returns(pet);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("pets.pet.not_found");
    }

    [Fact]
    public async Task Handle_Should_ReturnFailure_When_Pet_Has_Future_Appointments()
    {
        var clinicId = Guid.NewGuid();
        var tutorId = Guid.NewGuid();
        var pet = CreateValidPet(clinicId, tutorId);
        var command = new DeactivatePetCommand(clinicId, tutorId, pet.Id);

        _petRepositoryMock.GetByIdAsync(command.ClinicId, command.PetId, Arg.Any<CancellationToken>()).Returns(pet);
        _appointmentDependencyCheckerMock.HasFutureAppointmentsForPetAsync(command.ClinicId, pet.Id, Arg.Any<CancellationToken>()).Returns(true);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("pets.pet.has_future_appointments");
    }

    [Fact]
    public async Task Handle_Should_ReturnSuccess_And_Deactivate_Pet_When_Valid()
    {
        var clinicId = Guid.NewGuid();
        var tutorId = Guid.NewGuid();
        var pet = CreateValidPet(clinicId, tutorId);
        var command = new DeactivatePetCommand(clinicId, tutorId, pet.Id);

        _petRepositoryMock.GetByIdAsync(command.ClinicId, command.PetId, Arg.Any<CancellationToken>()).Returns(pet);
        _appointmentDependencyCheckerMock.HasFutureAppointmentsForPetAsync(command.ClinicId, pet.Id, Arg.Any<CancellationToken>()).Returns(false);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        pet.IsActive.Should().BeFalse();

        await _petRepositoryMock.Received(1).GetByIdAsync(command.ClinicId, command.PetId, Arg.Any<CancellationToken>());
    }
}