using FluentAssertions;
using NSubstitute;
using PetClinix.BuildingBlocks.Application;
using PetClinix.Modules.Pets.Application.Contracts;
using PetClinix.Modules.Pets.Application.UseCases.DeactivateTutor;
using PetClinix.Modules.Pets.Domain.Entities;
using PetClinix.Modules.Pets.Domain.Repositories;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace PetClinix.UnitTests.Handlers;

public class DeactivateTutorCommandHandlerTests
{
    private readonly ITutorRepository _tutorRepositoryMock;
    private readonly IPetsUnitOfWork _unitOfWorkMock;
    private readonly DeactivateTutorCommandHandler _handler;
    private readonly IPetDependencyChecker _petDependencyCheckerMock;
    private readonly IAppointmentDependencyChecker _appointmentDependencyCheckerMock;

    public DeactivateTutorCommandHandlerTests()
    {
        _tutorRepositoryMock = Substitute.For<ITutorRepository>();
        _unitOfWorkMock = Substitute.For<IPetsUnitOfWork>();
        _petDependencyCheckerMock = Substitute.For<IPetDependencyChecker>();
        _appointmentDependencyCheckerMock = Substitute.For<IAppointmentDependencyChecker>();

        _handler = new DeactivateTutorCommandHandler(
           _tutorRepositoryMock,
           _unitOfWorkMock,
           _petDependencyCheckerMock,
           _appointmentDependencyCheckerMock
        );
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
        var command = new DeactivateTutorCommand(Guid.NewGuid(), Guid.NewGuid());
        _tutorRepositoryMock.GetByIdAsync(command.ClinicId, command.TutorId, Arg.Any<CancellationToken>()).Returns((Tutor?)null);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("pets.tutor.not_found");
    }

    [Fact]
    public async Task Handle_Should_ReturnFailure_When_Tutor_Has_Active_Pets()
    {
        var clinicId = Guid.NewGuid();
        var tutor = CreateValidTutor(clinicId);
        var command = new DeactivateTutorCommand(clinicId, tutor.Id);

        _tutorRepositoryMock.GetByIdAsync(command.ClinicId, command.TutorId, Arg.Any<CancellationToken>()).Returns(tutor);
        _petDependencyCheckerMock.HasActivePetsByTutorAsync(command.ClinicId, tutor.Id, Arg.Any<CancellationToken>()).Returns(true);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("pets.tutor.has_active_pets");
    }

    [Fact]
    public async Task Handle_Should_ReturnFailure_When_Tutor_Has_Future_Appointments()
    {
        var clinicId = Guid.NewGuid();
        var tutor = CreateValidTutor(clinicId);
        var command = new DeactivateTutorCommand(clinicId, tutor.Id);

        _tutorRepositoryMock.GetByIdAsync(command.ClinicId, command.TutorId, Arg.Any<CancellationToken>()).Returns(tutor);
        _petDependencyCheckerMock.HasActivePetsByTutorAsync(command.ClinicId, tutor.Id, Arg.Any<CancellationToken>()).Returns(false);
        _appointmentDependencyCheckerMock.HasFutureAppointmentsForTutorAsync(command.ClinicId, tutor.Id, Arg.Any<CancellationToken>()).Returns(true);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("pets.tutor.has_future_appointments");
    }

    [Fact]
    public async Task Handle_Should_ReturnSuccess_And_Deactivate_When_No_Dependencies()
    {
        var clinicId = Guid.NewGuid();
        var tutor = CreateValidTutor(clinicId);
        var command = new DeactivateTutorCommand(clinicId, tutor.Id);

        _tutorRepositoryMock.GetByIdAsync(command.ClinicId, command.TutorId, Arg.Any<CancellationToken>()).Returns(tutor);
        _petDependencyCheckerMock.HasActivePetsByTutorAsync(command.ClinicId, tutor.Id, Arg.Any<CancellationToken>()).Returns(false);
        _appointmentDependencyCheckerMock.HasFutureAppointmentsForTutorAsync(command.ClinicId, tutor.Id, Arg.Any<CancellationToken>()).Returns(false);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        tutor.IsActive.Should().BeFalse();

        await _tutorRepositoryMock.Received(1).GetByIdAsync(command.ClinicId, command.TutorId, Arg.Any<CancellationToken>());
    }
}