using FluentAssertions;
using NSubstitute;
using PetClinix.Modules.Appointments.Application.Contracts;
using PetClinix.Modules.Appointments.Application.UseCases.RegisterAppointment;
using PetClinix.Modules.Appointments.Domain.Entities;
using PetClinix.Modules.Appointments.Domain.Repositories;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace PetClinix.UnitTests.Handlers;

public class RegisterAppointmentCommandHandlerTests
{
    private readonly IAppointmentRepository _appointmentRepositoryMock;
    private readonly IServiceCatalogService _serviceCatalogServiceMock;
    private readonly IClinicScheduleService _clinicScheduleServiceMock;
    private readonly IAppointmentsUnitOfWork _unitOfWorkMock;
    private readonly RegisterAppointmentCommandHandler _handler;

    public RegisterAppointmentCommandHandlerTests()
    {
        _appointmentRepositoryMock = Substitute.For<IAppointmentRepository>();
        _serviceCatalogServiceMock = Substitute.For<IServiceCatalogService>();
        _clinicScheduleServiceMock = Substitute.For<IClinicScheduleService>();
        _unitOfWorkMock = Substitute.For<IAppointmentsUnitOfWork>();
        _handler = new RegisterAppointmentCommandHandler(_appointmentRepositoryMock, _serviceCatalogServiceMock, _clinicScheduleServiceMock, _unitOfWorkMock);
    }

    private static RegisterAppointmentCommand CreateValidCommand(DateOnly? date = null, TimeOnly? time = null) => new(
        Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
        date ?? DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)), time ?? new TimeOnly(10, 0), "Observações", Guid.NewGuid()
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
    public async Task Handle_Should_ReturnFailure_When_Service_Duration_Is_Invalid()
    {
        var command = CreateValidCommand();

        SetupServiceDuration(0);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("appointments.appt.invalid_service");
        await _appointmentRepositoryMock.DidNotReceive().AddAsync(Arg.Any<Appointment>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_Should_ReturnFailure_When_Time_Is_Before_Working_Hours()
    {
        var command = CreateValidCommand(time: new TimeOnly(7, 30));

        SetupServiceDuration(30);
        SetupWorkingHours();

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("appointments.appt.outside_working_hours");
        await _appointmentRepositoryMock.DidNotReceive().AddAsync(Arg.Any<Appointment>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_Should_ReturnFailure_When_Time_Is_After_Working_Hours()
    {
        var command = CreateValidCommand(time: new TimeOnly(18, 30));

        SetupServiceDuration(30);
        SetupWorkingHours();

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("appointments.appt.outside_working_hours");
        await _appointmentRepositoryMock.DidNotReceive().AddAsync(Arg.Any<Appointment>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_Should_ReturnFailure_When_Appointment_Ends_After_Working_Hours()
    {
        var command = CreateValidCommand(time: new TimeOnly(17, 45));

        SetupServiceDuration(30);
        SetupWorkingHours();

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("appointments.appt.outside_working_hours");
        await _appointmentRepositoryMock.DidNotReceive().AddAsync(Arg.Any<Appointment>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_Should_ReturnSuccess_When_Appointment_Ends_Exactly_At_Working_Hours_End()
    {
        var command = CreateValidCommand(time: new TimeOnly(17, 30));

        SetupServiceDuration(30);
        SetupWorkingHours();
        SetupExistingDurations([]);
        SetupExistingAppointments([]);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        await _appointmentRepositoryMock.Received(1).AddAsync(Arg.Any<Appointment>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_Should_ReturnSuccess_When_Appointment_Starts_Exactly_At_Working_Hours_Start()
    {
        var command = CreateValidCommand(time: new TimeOnly(8, 0));

        SetupServiceDuration(30);
        SetupWorkingHours();
        SetupExistingDurations([]);
        SetupExistingAppointments([]);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        await _appointmentRepositoryMock.Received(1).AddAsync(Arg.Any<Appointment>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_Should_ReturnFailure_When_Slot_Is_Taken()
    {
        var command = CreateValidCommand();

        SetupServiceDuration(30);
        SetupWorkingHours();

        var existingServiceId = Guid.NewGuid();
        var existingAppointment = Appointment.Create(
            command.ClinicId, command.TutorId, command.PetId, existingServiceId, command.VeterinarianId,
            command.Date, command.Time, null, Guid.NewGuid()
        );

        SetupExistingDurations(new Dictionary<Guid, int> { [existingServiceId] = 30 });
        SetupExistingAppointments([existingAppointment]);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("appointments.appt.slot_taken");
        await _appointmentRepositoryMock.DidNotReceive().AddAsync(Arg.Any<Appointment>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_Should_ReturnSuccess_When_New_Appointment_Is_Adjacent_To_Existing_One()
    {
        var command = CreateValidCommand(time: new TimeOnly(10, 30));

        SetupServiceDuration(30);
        SetupWorkingHours();

        var existingServiceId = Guid.NewGuid();
        var existingAppointment = Appointment.Create(
            command.ClinicId, Guid.NewGuid(), Guid.NewGuid(), existingServiceId, command.VeterinarianId,
            command.Date, new TimeOnly(10, 0), null, Guid.NewGuid()
        );

        SetupExistingDurations(new Dictionary<Guid, int> { [existingServiceId] = 30 });
        SetupExistingAppointments([existingAppointment]);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        await _appointmentRepositoryMock.Received(1).AddAsync(Arg.Any<Appointment>(), Arg.Any<CancellationToken>());
        await _unitOfWorkMock.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_Should_ReturnFailure_When_Date_Is_In_The_Past()
    {
        var command = CreateValidCommand(date: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)));

        SetupServiceDuration(30);
        SetupWorkingHours();
        SetupExistingDurations([]);
        SetupExistingAppointments([]);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("appointments.appt.past_date");
        await _appointmentRepositoryMock.DidNotReceive().AddAsync(Arg.Any<Appointment>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_Should_ReturnSuccess_And_Save_Appointment_When_Valid()
    {
        var command = CreateValidCommand();

        SetupServiceDuration(30);
        SetupWorkingHours();
        SetupExistingDurations([]);
        SetupExistingAppointments([]);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        await _appointmentRepositoryMock.Received(1).AddAsync(Arg.Any<Appointment>(), Arg.Any<CancellationToken>());
        await _unitOfWorkMock.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}