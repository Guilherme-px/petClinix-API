using FluentAssertions;
using NSubstitute;
using PetClinix.Modules.Pets.Application.Contracts;
using PetClinix.Modules.Pets.Application.UseCases.RegisterPet;
using PetClinix.Modules.Pets.Domain.Entities;
using PetClinix.Modules.Pets.Domain.Enums;
using PetClinix.Modules.Pets.Domain.Repositories;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace PetClinix.UnitTests.Handlers;

public class RegisterPetCommandHandlerTests
{
    private readonly ITutorRepository _tutorRepositoryMock;
    private readonly IPetRepository _petRepositoryMock;
    private readonly IPetsUnitOfWork _unitOfWorkMock;
    private readonly RegisterPetCommandHandler _handler;

    public RegisterPetCommandHandlerTests()
    {
        _tutorRepositoryMock = Substitute.For<ITutorRepository>();
        _petRepositoryMock = Substitute.For<IPetRepository>();
        _unitOfWorkMock = Substitute.For<IPetsUnitOfWork>();
        _handler = new RegisterPetCommandHandler(_tutorRepositoryMock, _petRepositoryMock, _unitOfWorkMock);
    }

    private static Tutor CreateValidTutor(Guid clinicId)
    {
        return Tutor.Create(
            clinicId, Guid.NewGuid(), "Tutor Teste", "12345678900", null, "11999990000", null,
            "01001000", "Rua Teste", "123", "Centro", null, "Sao Paulo", "SP", null);
    }

    private static RegisterPetCommand CreateValidCommand(Guid clinicId, Guid tutorId) => new(
        clinicId, tutorId, Guid.NewGuid(), "Rex", Species.Dog, "Vira Lata",
        new DateOnly(2020, 5, 10), PetSex.Male, 15.5, true, "Nenhuma");

    [Fact]
    public async Task Handle_Should_ReturnFailure_When_Tutor_Does_Not_Exist()
    {
        var command = CreateValidCommand(Guid.NewGuid(), Guid.NewGuid());
        _tutorRepositoryMock.GetByIdAsync(command.ClinicId, command.TutorId, Arg.Any<CancellationToken>()).Returns((Tutor?)null);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("pets.tutor.not_found");
        await _petRepositoryMock.DidNotReceive().AddAsync(Arg.Any<Pet>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_Should_ReturnFailure_When_Tutor_From_Another_Clinic_Is_Not_Returned_By_Repository()
    {
        var myClinicId = Guid.NewGuid();
        var otherClinicId = Guid.NewGuid();
        var tutor = CreateValidTutor(otherClinicId);

        var command = CreateValidCommand(myClinicId, tutor.Id);

        _tutorRepositoryMock.GetByIdAsync(command.ClinicId, command.TutorId, Arg.Any<CancellationToken>()).Returns((Tutor?)null);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("pets.tutor.not_found");
        await _petRepositoryMock.DidNotReceive().AddAsync(Arg.Any<Pet>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_Should_ReturnFailure_When_Pet_Name_Duplicated()
    {
        var clinicId = Guid.NewGuid();
        var tutor = CreateValidTutor(clinicId);
        var command = CreateValidCommand(clinicId, tutor.Id);

        _tutorRepositoryMock.GetByIdAsync(command.ClinicId, command.TutorId, Arg.Any<CancellationToken>()).Returns(tutor);
        _petRepositoryMock.ExistsByNameAndTutorAsync(command.ClinicId, command.TutorId, command.Name, Arg.Any<CancellationToken>()).Returns(true);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("pets.pet.duplicate_name");
        await _petRepositoryMock.DidNotReceive().AddAsync(Arg.Any<Pet>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_Should_ReturnFailure_When_Domain_Throws_Exception()
    {
        var clinicId = Guid.NewGuid();
        var tutor = CreateValidTutor(clinicId);
        var command = CreateValidCommand(clinicId, tutor.Id) with { Name = "" };

        _tutorRepositoryMock.GetByIdAsync(command.ClinicId, command.TutorId, Arg.Any<CancellationToken>()).Returns(tutor);
        _petRepositoryMock.ExistsByNameAndTutorAsync(command.ClinicId, command.TutorId, command.Name, Arg.Any<CancellationToken>()).Returns(false);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("pets.pet.name_required");
        await _petRepositoryMock.DidNotReceive().AddAsync(Arg.Any<Pet>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_Should_ReturnSuccess_And_Save_Pet_When_Valid()
    {
        var clinicId = Guid.NewGuid();
        var tutor = CreateValidTutor(clinicId);
        var command = CreateValidCommand(clinicId, tutor.Id);

        _tutorRepositoryMock.GetByIdAsync(command.ClinicId, command.TutorId, Arg.Any<CancellationToken>()).Returns(tutor);
        _petRepositoryMock.ExistsByNameAndTutorAsync(command.ClinicId, command.TutorId, command.Name, Arg.Any<CancellationToken>()).Returns(false);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        await _tutorRepositoryMock.Received(1).GetByIdAsync(command.ClinicId, command.TutorId, Arg.Any<CancellationToken>());
        await _petRepositoryMock.Received(1).AddAsync(Arg.Any<Pet>(), Arg.Any<CancellationToken>());
        await _unitOfWorkMock.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}