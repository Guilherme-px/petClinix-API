using FluentAssertions;
using NSubstitute;
using PetClinix.Modules.Appointments.Application.Contracts;
using PetClinix.Modules.Appointments.Application.UseCases.UpdateAppointmentStatus;
using PetClinix.Modules.Appointments.Domain.Entities;
using PetClinix.Modules.Appointments.Domain.Enums;
using PetClinix.Modules.Appointments.Domain.Repositories;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace PetClinix.UnitTests.Handlers;

public class UpdateAppointmentStatusCommandHandlerTests
{
    private readonly IAppointmentRepository _appointmentRepositoryMock;
    private readonly IAppointmentsUnitOfWork _unitOfWorkMock;
    private readonly UpdateAppointmentStatusCommandHandler _handler;

    public UpdateAppointmentStatusCommandHandlerTests()
    {
        _appointmentRepositoryMock = Substitute.For<IAppointmentRepository>();
        _unitOfWorkMock = Substitute.For<IAppointmentsUnitOfWork>();
        _handler = new UpdateAppointmentStatusCommandHandler(_appointmentRepositoryMock, _unitOfWorkMock);
    }

    private static Appointment CreateValidAppointment(Guid clinicId)
    {
        return Appointment.Create(
            clinicId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)), new TimeOnly(10, 0), null, Guid.NewGuid()
        );
    }

    [Fact]
    public async Task Handle_Should_ReturnFailure_When_Appointment_Does_Not_Exist()
    {
        var command = new UpdateAppointmentStatusCommand(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), AppointmentStatus.Confirmed);
        _appointmentRepositoryMock.GetByIdAsync(command.AppointmentId, Arg.Any<CancellationToken>()).Returns((Appointment?)null);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("appointments.appt.not_found");
        await _unitOfWorkMock.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_Should_ReturnFailure_When_Status_Transition_Is_Invalid()
    {
        var clinicId = Guid.NewGuid();
        var appt = CreateValidAppointment(clinicId);
        appt.Cancel(Guid.NewGuid());

        var command = new UpdateAppointmentStatusCommand(clinicId, appt.Id, Guid.NewGuid(), AppointmentStatus.Confirmed);

        _appointmentRepositoryMock.GetByIdAsync(command.AppointmentId, Arg.Any<CancellationToken>()).Returns(appt);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("appointments.appt.invalid_status");
        await _unitOfWorkMock.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_Should_ReturnSuccess_And_Update_Status_When_Valid()
    {
        var clinicId = Guid.NewGuid();
        var appt = CreateValidAppointment(clinicId);

        var command = new UpdateAppointmentStatusCommand(clinicId, appt.Id, Guid.NewGuid(), AppointmentStatus.Confirmed);

        _appointmentRepositoryMock.GetByIdAsync(command.AppointmentId, Arg.Any<CancellationToken>()).Returns(appt);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        appt.Status.Should().Be(AppointmentStatus.Confirmed);

        await _appointmentRepositoryMock.Received(1).UpdateAsync(Arg.Any<Appointment>(), Arg.Any<CancellationToken>());
        await _unitOfWorkMock.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}