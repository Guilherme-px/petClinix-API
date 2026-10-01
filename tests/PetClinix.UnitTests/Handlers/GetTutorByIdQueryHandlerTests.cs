using FluentAssertions;
using NSubstitute;
using PetClinix.Modules.Pets.Application.UseCases.GetTutors;
using PetClinix.Modules.Pets.Domain.Entities;
using PetClinix.Modules.Pets.Domain.Repositories;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace PetClinix.UnitTests.Handlers;

public class GetTutorByIdQueryHandlerTests
{
    private readonly ITutorRepository _tutorRepositoryMock;
    private readonly GetTutorByIdQueryHandler _handler;

    public GetTutorByIdQueryHandlerTests()
    {
        _tutorRepositoryMock = Substitute.For<ITutorRepository>();
        _handler = new GetTutorByIdQueryHandler(_tutorRepositoryMock);
    }

    private static Tutor CreateValidTutor(Guid clinicId)
    {
        return Tutor.Create(
            clinicId, Guid.NewGuid(), "Tutor Teste", "12345678900", null, "11999990000", null,
            "01001000", "Rua Teste", "123", "Centro", null, "Sao Paulo", "SP", null
        );
    }

    [Fact]
    public async Task Handle_Should_ReturnFailure_When_Tutor_Does_Not_Exist()
    {
        var query = new GetTutorByIdQuery(Guid.NewGuid(), Guid.NewGuid());
        _tutorRepositoryMock.GetByIdAsync(query.TutorId, Arg.Any<CancellationToken>()).Returns((Tutor?)null);

        var result = await _handler.Handle(query, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("pets.tutor.not_found");
    }

    [Fact]
    public async Task Handle_Should_ReturnFailure_When_Tutor_Belongs_To_Another_Clinic()
    {
        var myClinicId = Guid.NewGuid();
        var otherClinicId = Guid.NewGuid();
        var tutor = CreateValidTutor(otherClinicId);

        var query = new GetTutorByIdQuery(myClinicId, tutor.Id);
        _tutorRepositoryMock.GetByIdAsync(query.TutorId, Arg.Any<CancellationToken>()).Returns(tutor);

        var result = await _handler.Handle(query, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("pets.tutor.not_found");
    }

    [Fact]
    public async Task Handle_Should_ReturnSuccess_When_Tutor_Belongs_To_Clinic()
    {
        var clinicId = Guid.NewGuid();
        var tutor = CreateValidTutor(clinicId);

        var query = new GetTutorByIdQuery(clinicId, tutor.Id);
        _tutorRepositoryMock.GetByIdAsync(query.TutorId, Arg.Any<CancellationToken>()).Returns(tutor);

        var result = await _handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Id.Should().Be(tutor.Id);
        result.Value.Name.Should().Be(tutor.Name);
    }

    [Fact]
    public async Task Handle_Should_Map_All_Fields_Correctly_When_Success()
    {
        var clinicId = Guid.NewGuid();
        var tutor = Tutor.Create(
            clinicId, Guid.NewGuid(), "Tutor Mapeamento", "12345678900", "tutor@teste.com", "11999990000", "11988887777",
            "01001000", "Rua Distinta", "42", "Bairro Diferente", "Apto 7", "Cidade Única", "RJ", "Observação única"
        );

        var query = new GetTutorByIdQuery(clinicId, tutor.Id);
        _tutorRepositoryMock.GetByIdAsync(query.TutorId, Arg.Any<CancellationToken>()).Returns(tutor);

        var result = await _handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Id.Should().Be(tutor.Id);
        result.Value.Name.Should().Be("Tutor Mapeamento");
        result.Value.Cpf.Should().Be("12345678900");
        result.Value.Email.Should().Be("tutor@teste.com");
        result.Value.PhoneNumber.Should().Be("11999990000");
        result.Value.SecondaryPhoneNumber.Should().Be("11988887777");
        result.Value.ZipCode.Should().Be("01001000");
        result.Value.Street.Should().Be("Rua Distinta");
        result.Value.Number.Should().Be("42");
        result.Value.Neighborhood.Should().Be("Bairro Diferente");
        result.Value.Complement.Should().Be("Apto 7");
        result.Value.City.Should().Be("Cidade Única");
        result.Value.State.Should().Be("RJ");
        result.Value.Notes.Should().Be("Observação única");
        result.Value.IsActive.Should().BeTrue();
    }
}