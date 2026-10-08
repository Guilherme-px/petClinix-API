using FluentAssertions;
using NSubstitute;
using PetClinix.Modules.Appointments.Application.Contracts;
using PetClinix.Modules.Appointments.Application.UseCases.UpdateAppointment;
using PetClinix.Modules.Appointments.Domain.Entities;
using PetClinix.Modules.Appointments.Domain.Repositories;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace PetClinix.UnitTests.Handlers;

public class UpdateAppointmentCommandHandlerTests
{
    private readonly IAppointmentRepository _appointmentRepositoryMock;
    private readonly IServiceCatalogService _serviceCatalogServiceMock;
    private readonly IClinicScheduleService _clinicScheduleServiceMock;
    private readonly IAppointmentsUnitOfWork _unitOfWorkMock;
    private readonly UpdateAppointmentCommandHandler _handler;

    public UpdateAppointmentCommandHandlerTests()
    {
        _appointmentRepositoryMock = Substitute.For<IAppointmentRepository>();
        _serviceCatalogServiceMock = Substitute.For<IServiceCatalogService>();
        _clinicScheduleServiceMock = Substitute.For<IClinicScheduleService>();
        _unitOfWorkMock = Substitute.For<IAppointmentsUnitOfWork>();
        _handler = new UpdateAppointmentCommandHandler(_appointmentRepositoryMock, _serviceCatalogServiceMock, _clinicScheduleServiceMock, _unitOfWorkMock);
    }

    private static Appointment CreateValidAppointment(Guid clinicId)
    {
        return Appointment.Create(
            clinicId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)), new TimeOnly(10, 0), "Original", Guid.NewGuid()
        );
    }

    private static UpdateAppointmentCommand CreateValidCommand(Guid clinicId, Guid apptId, Guid vetId, TimeOnly? time = null) => new(
        clinicId, apptId, Guid.NewGuid(), vetId, Guid.NewGuid(),
        DateOnly.FromDateTime(DateTime.UtcNow.AddDays(2)), time ?? new TimeOnly(10, 0), "Notas atualizadas"
    );

    private void SetupServiceDuration(int durationMinutes)
    {
        _serviceCatalogServiceMock.GetDurationInMinutesAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(durationMinutes);
    }

    private void SetupWorkingHours()
    {
        _clinicScheduleServiceMock.GetWorkingHours().Returns((new TimeOnly(8, 0), new TimeOnly(18, 0)));
    }

    private void SetupExistingDurations(Dictionary<Guid, int> durations)
    {
        _serviceCatalogServiceMock.GetDurationsInMinutesAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(durations);
    }

    private void SetupExistingAppointments(List<Appointment> appointments)
    {
        _appointmentRepositoryMock.GetByVeterinarianAndDateAsync(Arg.Any<Guid>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(appointments);
    }

    [Fact]
    public async Task Handle_Should_ReturnFailure_When_Appointment_Does_Not_Exist()
    {
        var command = CreateValidCommand(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        _appointmentRepositoryMock.GetByIdAsync(command.AppointmentId, Arg.Any<CancellationToken>()).Returns((Appointment?)null);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("appointments.appt.not_found");
        await _unitOfWorkMock.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_Should_ReturnFailure_When_Time_Is_Outside_Working_Hours()
    {
        var clinicId = Guid.NewGuid();
        var vetId = Guid.NewGuid();
        var appt = CreateValidAppointment(clinicId);

        var command = CreateValidCommand(clinicId, appt.Id, vetId, new TimeOnly(19, 0));

        _appointmentRepositoryMock.GetByIdAsync(command.AppointmentId, Arg.Any<CancellationToken>()).Returns(appt);
        SetupServiceDuration(30);
        SetupWorkingHours();

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("appointments.appt.outside_working_hours");
        await _unitOfWorkMock.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_Should_ReturnFailure_When_Appointment_Ends_After_Working_Hours()
    {
        var clinicId = Guid.NewGuid();
        var vetId = Guid.NewGuid();
        var appt = CreateValidAppointment(clinicId);

        var command = CreateValidCommand(clinicId, appt.Id, vetId, new TimeOnly(17, 45));

        _appointmentRepositoryMock.GetByIdAsync(command.AppointmentId, Arg.Any<CancellationToken>()).Returns(appt);
        SetupServiceDuration(30);
        SetupWorkingHours();

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("appointments.appt.outside_working_hours");
        await _unitOfWorkMock.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_Should_ReturnFailure_When_Double_Booking()
    {
        var clinicId = Guid.NewGuid();
        var vetId = Guid.NewGuid();
        var appt = CreateValidAppointment(clinicId);

        var command = CreateValidCommand(clinicId, appt.Id, vetId);

        var conflictingServiceId = Guid.NewGuid();
        var conflictingAppt = Appointment.Create(
            clinicId, Guid.NewGuid(), Guid.NewGuid(), conflictingServiceId, vetId,
            command.Date, command.Time, null, Guid.NewGuid());

        _appointmentRepositoryMock.GetByIdAsync(command.AppointmentId, Arg.Any<CancellationToken>()).Returns(appt);
        SetupServiceDuration(30);
        SetupWorkingHours();
        SetupExistingDurations(new Dictionary<Guid, int> { [conflictingServiceId] = 30 });
        SetupExistingAppointments([conflictingAppt]);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("appointments.appt.slot_taken");
        await _unitOfWorkMock.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_Should_ReturnFailure_When_Appointment_Is_Completed()
    {
        var clinicId = Guid.NewGuid();
        var vetId = Guid.NewGuid();
        var appt = CreateValidAppointment(clinicId);
        appt.Complete(Guid.NewGuid());

        var command = CreateValidCommand(clinicId, appt.Id, vetId);

        _appointmentRepositoryMock.GetByIdAsync(command.AppointmentId, Arg.Any<CancellationToken>()).Returns(appt);
        SetupServiceDuration(30);
        SetupWorkingHours();
        SetupExistingDurations([]);
        SetupExistingAppointments([]);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("appointments.appt.invalid_status");
        await _unitOfWorkMock.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_Should_ReturnSuccess_And_Update_Appointment_When_Valid()
    {
        var clinicId = Guid.NewGuid();
        var vetId = Guid.NewGuid();
        var appt = CreateValidAppointment(clinicId);

        var command = CreateValidCommand(clinicId, appt.Id, vetId);

        _appointmentRepositoryMock.GetByIdAsync(command.AppointmentId, Arg.Any<CancellationToken>()).Returns(appt);
        SetupServiceDuration(30);
        SetupWorkingHours();
        SetupExistingDurations([]);
        SetupExistingAppointments([]);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        appt.Notes.Should().Be(command.Notes);
        appt.VeterinarianId.Should().Be(command.VeterinarianId);
        appt.ServiceId.Should().Be(command.ServiceId);
        appt.ScheduledDate.Should().Be(command.Date);
        appt.ScheduledTime.Should().Be(command.Time);
        appt.UpdatedByUserId.Should().Be(command.UpdatedByUserId);

        await _appointmentRepositoryMock.Received(1).UpdateAsync(Arg.Any<Appointment>(), Arg.Any<CancellationToken>());
        await _unitOfWorkMock.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}