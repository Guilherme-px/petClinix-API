using FluentAssertions;
using NSubstitute;
using PetClinix.Modules.Pets.Application.UseCases.GetPets;
using PetClinix.Modules.Pets.Domain.Entities;
using PetClinix.Modules.Pets.Domain.Enums;
using PetClinix.Modules.Pets.Domain.Repositories;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace PetClinix.UnitTests.Handlers;

public class GetPetsQueryHandlerTests
{
    private readonly IPetRepository _petRepositoryMock;
    private readonly GetPetsQueryHandler _handler;

    public GetPetsQueryHandlerTests()
    {
        _petRepositoryMock = Substitute.For<IPetRepository>();
        _handler = new GetPetsQueryHandler(_petRepositoryMock);
    }

    private static Pet CreateValidPet(Guid clinicId, Guid tutorId)
    {
        return Pet.Create(
            clinicId, tutorId, Guid.NewGuid(), "Rex", Species.Dog, "Vira Lata",
            new DateOnly(2020, 5, 10), PetSex.Male, 15.5, true, null);
    }

    [Fact]
    public async Task Handle_Should_Return_PagedResult_With_Correct_Data()
    {
        var clinicId = Guid.NewGuid();
        var tutorId = Guid.NewGuid();
        var query = new GetPetsQuery(clinicId, tutorId, 1, 10);

        var pets = new List<Pet>
        {
            CreateValidPet(clinicId, tutorId),
            CreateValidPet(clinicId, tutorId)
        };

        _petRepositoryMock.GetAllByTutorIdAsync(
            clinicId, tutorId, 1, 10, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((pets, 2));

        var result = await _handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Items.Should().HaveCount(2);
        result.Value.TotalCount.Should().Be(2);
        result.Value.PageNumber.Should().Be(1);
        result.Value.PageSize.Should().Be(10);
    }

    [Fact]
    public async Task Handle_Should_Return_Empty_List_When_No_Pets_Exist()
    {
        var clinicId = Guid.NewGuid();
        var tutorId = Guid.NewGuid();
        var query = new GetPetsQuery(clinicId, tutorId, 1, 10);

        _petRepositoryMock.GetAllByTutorIdAsync(
            clinicId, tutorId, 1, 10, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((new List<Pet>(), 0));

        var result = await _handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Items.Should().BeEmpty();
        result.Value.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_Should_Pass_Search_Term_To_Repository()
    {
        var clinicId = Guid.NewGuid();
        var tutorId = Guid.NewGuid();
        var query = new GetPetsQuery(clinicId, tutorId, 1, 10, "rex");

        _petRepositoryMock.GetAllByTutorIdAsync(
            clinicId, tutorId, 1, 10, "rex", Arg.Any<CancellationToken>())
            .Returns((new List<Pet>(), 0));

        await _handler.Handle(query, CancellationToken.None);

        await _petRepositoryMock.Received(1).GetAllByTutorIdAsync(
            clinicId, tutorId, 1, 10, "rex", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_Should_Pass_Empty_Search_When_Not_Provided()
    {
        var clinicId = Guid.NewGuid();
        var tutorId = Guid.NewGuid();
        var query = new GetPetsQuery(clinicId, tutorId, 1, 10);

        _petRepositoryMock.GetAllByTutorIdAsync(
            clinicId, tutorId, 1, 10, "", Arg.Any<CancellationToken>())
            .Returns((new List<Pet>(), 0));

        await _handler.Handle(query, CancellationToken.None);

        await _petRepositoryMock.Received(1).GetAllByTutorIdAsync(
            clinicId, tutorId, 1, 10, "", Arg.Any<CancellationToken>());
    }
}