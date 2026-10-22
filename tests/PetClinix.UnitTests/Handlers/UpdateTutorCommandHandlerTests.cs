using FluentAssertions;
using NSubstitute;
using PetClinix.Modules.Pets.Application.Contracts;
using PetClinix.Modules.Pets.Application.UseCases.UpdateTutor;
using PetClinix.Modules.Pets.Domain.Entities;
using PetClinix.Modules.Pets.Domain.Repositories;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace PetClinix.UnitTests.Handlers;

public class UpdateTutorCommandHandlerTests
{
    private readonly ITutorRepository _tutorRepositoryMock;
    private readonly IPetsUnitOfWork _unitOfWorkMock;
    private readonly UpdateTutorCommandHandler _handler;

    public UpdateTutorCommandHandlerTests()
    {
        _tutorRepositoryMock = Substitute.For<ITutorRepository>();
        _unitOfWorkMock = Substitute.For<IPetsUnitOfWork>();
        _handler = new UpdateTutorCommandHandler(_tutorRepositoryMock, _unitOfWorkMock);
    }

    private static Tutor CreateValidTutor(Guid clinicId)
    {
        return Tutor.Create(
            clinicId, Guid.NewGuid(), "Tutor Original", "12345678900", null, "11999990000", null,
            "01001000", "Rua Antiga", "123", "Centro", null, "Sao Paulo", "SP", null
        );
    }

    private static UpdateTutorCommand CreateValidCommand(Guid clinicId, Guid tutorId) => new(
        clinicId, tutorId, "Tutor Atualizado", "novo@email.com", "1188887777", null,
        "01001000", "Rua Nova", "999", "Bairro Novo", null, "Santos", "SP", "Notas novas"
    );

    [Fact]
    public async Task Handle_Should_ReturnFailure_When_Tutor_Does_Not_Exist()
    {
        var command = CreateValidCommand(Guid.NewGuid(), Guid.NewGuid());
        _tutorRepositoryMock.GetByIdAsync(command.ClinicId, command.TutorId, Arg.Any<CancellationToken>()).Returns((Tutor?)null);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("pets.tutor.not_found");
        await _unitOfWorkMock.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
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
        await _unitOfWorkMock.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_Should_ReturnFailure_When_Domain_Throws_Exception()
    {
        var clinicId = Guid.NewGuid();
        var tutor = CreateValidTutor(clinicId);
        var command = CreateValidCommand(clinicId, tutor.Id) with { Name = "" };

        _tutorRepositoryMock.GetByIdAsync(command.ClinicId, command.TutorId, Arg.Any<CancellationToken>()).Returns(tutor);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("pets.tutor.name_required");
        await _unitOfWorkMock.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_Should_ReturnSuccess_And_Update_Tutor_When_Valid()
    {
        var clinicId = Guid.NewGuid();
        var tutor = CreateValidTutor(clinicId);
        var command = CreateValidCommand(clinicId, tutor.Id);

        _tutorRepositoryMock.GetByIdAsync(command.ClinicId, command.TutorId, Arg.Any<CancellationToken>()).Returns(tutor);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        tutor.Name.Should().Be(command.Name);
        tutor.Street.Should().Be(command.Street);

        await _tutorRepositoryMock.Received(1).GetByIdAsync(command.ClinicId, command.TutorId, Arg.Any<CancellationToken>());
        await _tutorRepositoryMock.Received(1).UpdateAsync(Arg.Any<Tutor>(), Arg.Any<CancellationToken>());
        await _unitOfWorkMock.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}