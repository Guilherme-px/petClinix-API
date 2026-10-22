using FluentAssertions;
using NSubstitute;
using PetClinix.Modules.Pets.Application.UseCases.GetPets;
using PetClinix.Modules.Pets.Domain.Entities;
using PetClinix.Modules.Pets.Domain.Enums;
using PetClinix.Modules.Pets.Domain.Repositories;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace PetClinix.UnitTests.Handlers;

public class GetPetByIdQueryHandlerTests
{
    private readonly IPetRepository _petRepositoryMock;
    private readonly GetPetByIdQueryHandler _handler;

    public GetPetByIdQueryHandlerTests()
    {
        _petRepositoryMock = Substitute.For<IPetRepository>();
        _handler = new GetPetByIdQueryHandler(_petRepositoryMock);
    }

    private static Pet CreateValidPet(Guid clinicId, Guid tutorId)
    {
        return Pet.Create(
            clinicId, tutorId, Guid.NewGuid(), "Rex", Species.Dog, "Vira Lata",
            new DateOnly(2020, 5, 10), PetSex.Male, 15.5, true, null);
    }

    [Fact]
    public async Task Handle_Should_ReturnFailure_When_Pet_Does_Not_Exist()
    {
        var query = new GetPetByIdQuery(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        _petRepositoryMock.GetByIdAsync(query.ClinicId, query.PetId, Arg.Any<CancellationToken>()).Returns((Pet?)null);

        var result = await _handler.Handle(query, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("pets.pet.not_found");
    }

    [Fact]
    public async Task Handle_Should_ReturnFailure_When_Pet_From_Another_Clinic_Is_Not_Returned_By_Repository()
    {
        var myClinicId = Guid.NewGuid();
        var otherClinicId = Guid.NewGuid();
        var tutorId = Guid.NewGuid();

        var pet = CreateValidPet(otherClinicId, tutorId);

        var query = new GetPetByIdQuery(myClinicId, tutorId, pet.Id);

        _petRepositoryMock.GetByIdAsync(query.ClinicId, query.PetId, Arg.Any<CancellationToken>()).Returns((Pet?)null);

        var result = await _handler.Handle(query, CancellationToken.None);

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

        var query = new GetPetByIdQuery(clinicId, myTutorId, pet.Id);
        _petRepositoryMock.GetByIdAsync(query.ClinicId, query.PetId, Arg.Any<CancellationToken>()).Returns(pet);

        var result = await _handler.Handle(query, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("pets.pet.not_found");
    }

    [Fact]
    public async Task Handle_Should_ReturnSuccess_When_Pet_Belongs_To_Tutor_And_Clinic()
    {
        var clinicId = Guid.NewGuid();
        var tutorId = Guid.NewGuid();
        var pet = CreateValidPet(clinicId, tutorId);

        var query = new GetPetByIdQuery(clinicId, tutorId, pet.Id);
        _petRepositoryMock.GetByIdAsync(query.ClinicId, query.PetId, Arg.Any<CancellationToken>()).Returns(pet);

        var result = await _handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Id.Should().Be(pet.Id);
        result.Value.Name.Should().Be(pet.Name);

        await _petRepositoryMock.Received(1).GetByIdAsync(query.ClinicId, query.PetId, Arg.Any<CancellationToken>());
    }
}