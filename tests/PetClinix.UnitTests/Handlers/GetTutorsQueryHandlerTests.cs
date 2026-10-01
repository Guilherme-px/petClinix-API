using FluentAssertions;
using NSubstitute;
using PetClinix.Modules.Pets.Application.UseCases.GetTutors;
using PetClinix.Modules.Pets.Domain.Entities;
using PetClinix.Modules.Pets.Domain.Repositories;
using PetClinix.Modules.Pets.Domain.ValueObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace PetClinix.UnitTests.Handlers;

public class GetTutorsQueryHandlerTests
{
    private readonly ITutorRepository _tutorRepositoryMock;
    private readonly GetTutorsQueryHandler _handler;

    public GetTutorsQueryHandlerTests()
    {
        _tutorRepositoryMock = Substitute.For<ITutorRepository>();
        _handler = new GetTutorsQueryHandler(_tutorRepositoryMock);
    }

    private static Tutor CreateValidTutor(Guid clinicId)
    {
        return Tutor.Create(
            clinicId, Guid.NewGuid(), "Tutor Teste", "12345678900", null, "11999990000", null,
            "01001000", "Rua Teste", "123", "Centro", null, "Sao Paulo", "SP", null
        );
    }

    private static Tutor CreateDistinctTutor(Guid clinicId)
    {
        return Tutor.Create(
            clinicId, Guid.NewGuid(), "Tutor Lista", "98765432100", "lista@teste.com", "11988887777", "1177776666",
            "01002000", "Rua Listagem", "77", "Bairro Listagem", "Casa 2", "Cidade Listagem", "MG", "Nota da listagem"
        );
    }

    [Fact]
    public async Task Handle_Should_Return_PagedResult_With_Correct_Data()
    {
        var clinicId = Guid.NewGuid();
        var query = new GetTutorsQuery(clinicId, 1, 10);

        var tutors = new List<Tutor>
        {
            CreateValidTutor(clinicId),
            CreateValidTutor(clinicId)
        };

        _tutorRepositoryMock.GetAllByClinicIdAsync(
            clinicId, 1, 10, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((tutors, 2));

        var result = await _handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Items.Should().HaveCount(2);
        result.Value.TotalCount.Should().Be(2);
        result.Value.PageNumber.Should().Be(1);
        result.Value.PageSize.Should().Be(10);
    }

    [Fact]
    public async Task Handle_Should_Return_Empty_List_When_No_Tutors_Exist()
    {
        var clinicId = Guid.NewGuid();
        var query = new GetTutorsQuery(clinicId, 1, 10);

        _tutorRepositoryMock.GetAllByClinicIdAsync(
            clinicId, 1, 10, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((new List<Tutor>(), 0));

        var result = await _handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Items.Should().BeEmpty();
        result.Value.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_Should_Map_All_Fields_Correctly()
    {
        var clinicId = Guid.NewGuid();
        var tutor = CreateDistinctTutor(clinicId);

        var query = new GetTutorsQuery(clinicId, 1, 10);
        _tutorRepositoryMock.GetAllByClinicIdAsync(
            clinicId, 1, 10, "", Arg.Any<CancellationToken>())
            .Returns((new List<Tutor> { tutor }, 1));

        var result = await _handler.Handle(query, CancellationToken.None);

        var item = result.Value!.Items.Single();
        item.Id.Should().Be(tutor.Id);
        item.Name.Should().Be("Tutor Lista");
        item.Cpf.Should().Be("98765432100");
        item.Email.Should().Be("lista@teste.com");
        item.PhoneNumber.Should().Be("11988887777");
        item.SecondaryPhoneNumber.Should().Be("1177776666");
        item.ZipCode.Should().Be("01002000");
        item.Street.Should().Be("Rua Listagem");
        item.Number.Should().Be("77");
        item.Neighborhood.Should().Be("Bairro Listagem");
        item.Complement.Should().Be("Casa 2");
        item.City.Should().Be("Cidade Listagem");
        item.State.Should().Be("MG");
        item.Notes.Should().Be("Nota da listagem");
        item.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_Should_Pass_Search_Term_To_Repository()
    {
        var clinicId = Guid.NewGuid();
        var query = new GetTutorsQuery(clinicId, 1, 10, "maria");

        _tutorRepositoryMock.GetAllByClinicIdAsync(
            clinicId, 1, 10, "maria", Arg.Any<CancellationToken>())
            .Returns((new List<Tutor>(), 0));

        await _handler.Handle(query, CancellationToken.None);

        await _tutorRepositoryMock.Received(1).GetAllByClinicIdAsync(
            clinicId, 1, 10, "maria", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_Should_Pass_Empty_Search_When_Not_Provided()
    {
        var clinicId = Guid.NewGuid();
        var query = new GetTutorsQuery(clinicId, 1, 10);

        _tutorRepositoryMock.GetAllByClinicIdAsync(
            clinicId, 1, 10, "", Arg.Any<CancellationToken>())
            .Returns((new List<Tutor>(), 0));

        await _handler.Handle(query, CancellationToken.None);

        await _tutorRepositoryMock.Received(1).GetAllByClinicIdAsync(
            clinicId, 1, 10, "", Arg.Any<CancellationToken>());
    }
}