using FluentAssertions;
using NSubstitute;
using PetClinix.Modules.Pets.Application.Contracts;
using PetClinix.Modules.Pets.Application.UseCases.UpdatePet;
using PetClinix.Modules.Pets.Domain.Entities;
using PetClinix.Modules.Pets.Domain.Enums;
using PetClinix.Modules.Pets.Domain.Repositories;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace PetClinix.UnitTests.Handlers;

public class UpdatePetCommandHandlerTests
{
    private readonly IPetRepository _petRepositoryMock;
    private readonly IPetsUnitOfWork _unitOfWorkMock;
    private readonly UpdatePetCommandHandler _handler;

    public UpdatePetCommandHandlerTests()
    {
        _petRepositoryMock = Substitute.For<IPetRepository>();
        _unitOfWorkMock = Substitute.For<IPetsUnitOfWork>();
        _handler = new UpdatePetCommandHandler(_petRepositoryMock, _unitOfWorkMock);
    }

    private static Pet CreateValidPet(Guid clinicId, Guid tutorId)
    {
        return Pet.Create(
            clinicId, tutorId, Guid.NewGuid(), "Rex Original", Species.Dog, "Vira Lata",
            new DateOnly(2020, 5, 10), PetSex.Male, 15.0, true, null);
    }

    private static UpdatePetCommand CreateValidCommand(Guid clinicId, Guid tutorId, Guid petId) => new(
        clinicId, tutorId, petId, Guid.NewGuid(), "Rex Atualizado", Species.Dog, "Labrador",
        new DateOnly(2020, 5, 10), PetSex.Male, 16.5, true, "Atualizado");

    [Fact]
    public async Task Handle_Should_ReturnFailure_When_Pet_Does_Not_Exist()
    {
        var command = CreateValidCommand(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        _petRepositoryMock.GetByIdAsync(command.ClinicId, command.PetId, Arg.Any<CancellationToken>()).Returns((Pet?)null);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("pets.pet.not_found");
        await _unitOfWorkMock.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_Should_ReturnFailure_When_Pet_From_Another_Clinic_Is_Not_Returned_By_Repository()
    {
        var myClinicId = Guid.NewGuid();
        var myTutorId = Guid.NewGuid();
        var otherClinicId = Guid.NewGuid();
        var otherTutorId = Guid.NewGuid();

        var pet = CreateValidPet(otherClinicId, otherTutorId);

        var command = CreateValidCommand(myClinicId, myTutorId, pet.Id);

        _petRepositoryMock.GetByIdAsync(command.ClinicId, command.PetId, Arg.Any<CancellationToken>()).Returns((Pet?)null);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("pets.pet.not_found");
        await _unitOfWorkMock.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_Should_ReturnFailure_When_Pet_Belongs_To_Another_Tutor()
    {
        var clinicId = Guid.NewGuid();
        var myTutorId = Guid.NewGuid();
        var otherTutorId = Guid.NewGuid();

        var pet = CreateValidPet(clinicId, otherTutorId);

        var command = CreateValidCommand(clinicId, myTutorId, pet.Id);
        _petRepositoryMock.GetByIdAsync(command.ClinicId, command.PetId, Arg.Any<CancellationToken>()).Returns(pet);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("pets.pet.not_found");
        await _unitOfWorkMock.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_Should_ReturnFailure_When_Domain_Throws_Exception()
    {
        var clinicId = Guid.NewGuid();
        var tutorId = Guid.NewGuid();
        var pet = CreateValidPet(clinicId, tutorId);

        var command = CreateValidCommand(clinicId, tutorId, pet.Id) with { Name = "" };

        _petRepositoryMock.GetByIdAsync(command.ClinicId, command.PetId, Arg.Any<CancellationToken>()).Returns(pet);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("pets.pet.name_required");
        await _unitOfWorkMock.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_Should_ReturnSuccess_And_Update_Pet_When_Valid()
    {
        var clinicId = Guid.NewGuid();
        var tutorId = Guid.NewGuid();
        var pet = CreateValidPet(clinicId, tutorId);
        var command = CreateValidCommand(clinicId, tutorId, pet.Id);

        _petRepositoryMock.GetByIdAsync(command.ClinicId, command.PetId, Arg.Any<CancellationToken>()).Returns(pet);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        pet.Name.Should().Be(command.Name);
        pet.Breed.Should().Be(command.Breed);
        pet.UpdatedByUserId.Should().Be(command.UpdatedByUserId);

        await _petRepositoryMock.Received(1).GetByIdAsync(command.ClinicId, command.PetId, Arg.Any<CancellationToken>());
        await _petRepositoryMock.Received(1).UpdateAsync(Arg.Any<Pet>(), Arg.Any<CancellationToken>());
        await _unitOfWorkMock.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}