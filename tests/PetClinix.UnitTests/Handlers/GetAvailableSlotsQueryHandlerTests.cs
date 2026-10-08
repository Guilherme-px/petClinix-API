using FluentAssertions;
using NSubstitute;
using PetClinix.Modules.Appointments.Application.Contracts;
using PetClinix.Modules.Appointments.Application.UseCases.GetAvailableSlots;
using PetClinix.Modules.Appointments.Domain.Entities;
using PetClinix.Modules.Appointments.Domain.Repositories;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace PetClinix.UnitTests.Handlers;

public class GetAvailableSlotsQueryHandlerTests
{
    private readonly IAppointmentRepository _appointmentRepositoryMock;
    private readonly IServiceCatalogService _serviceCatalogServiceMock;
    private readonly IClinicScheduleService _clinicScheduleServiceMock;
    private readonly GetAvailableSlotsQueryHandler _handler;

    public GetAvailableSlotsQueryHandlerTests()
    {
        _appointmentRepositoryMock = Substitute.For<IAppointmentRepository>();
        _serviceCatalogServiceMock = Substitute.For<IServiceCatalogService>();
        _clinicScheduleServiceMock = Substitute.For<IClinicScheduleService>();
        _handler = new GetAvailableSlotsQueryHandler(_appointmentRepositoryMock, _serviceCatalogServiceMock, _clinicScheduleServiceMock);
    }

    private static DateOnly FutureDate() => DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1));

    private void SetupWorkingHours()
    {
        _clinicScheduleServiceMock.GetWorkingHours().Returns((new TimeOnly(8, 0), new TimeOnly(10, 0)));
    }

    private void SetupQueryServiceDuration(int durationMinutes)
    {
        _serviceCatalogServiceMock.GetDurationInMinutesAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(durationMinutes);
    }

    private void SetupExistingDurations(Dictionary<Guid, int> durations)
    {
        _serviceCatalogServiceMock.GetDurationsInMinutesAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(durations);
    }

    private void SetupAppointments(List<Appointment> appointments)
    {
        _appointmentRepositoryMock.GetByVeterinarianAndDateAsync(Arg.Any<Guid>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(appointments);
    }

    [Fact]
    public async Task Handle_Should_Return_All_Slots_When_No_Appointments_Exist()
    {
        var date = FutureDate();
        var query = new GetAvailableSlotsQuery(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), date);

        SetupWorkingHours();
        SetupQueryServiceDuration(30);
        SetupExistingDurations([]);
        SetupAppointments([]);

        var result = await _handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Should().BeEquivalentTo(new List<string> { "08:00", "08:30", "09:00", "09:30" });
    }

    [Fact]
    public async Task Handle_Should_Skip_Conflicting_Slot_When_Appointment_Exists()
    {
        var date = FutureDate();
        var query = new GetAvailableSlotsQuery(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), date);

        SetupWorkingHours();
        SetupQueryServiceDuration(30);

        var existingServiceId = Guid.NewGuid();
        var existingAppt = Appointment.Create(
            query.ClinicId, Guid.NewGuid(), Guid.NewGuid(), existingServiceId, query.VeterinarianId,
            date, new TimeOnly(8, 30), null, Guid.NewGuid());

        SetupExistingDurations(new Dictionary<Guid, int> { [existingServiceId] = 30 });
        SetupAppointments([existingAppt]);

        var result = await _handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEquivalentTo(new List<string> { "08:00", "09:00", "09:30" });
        result.Value.Should().NotContain("08:30");
    }

    [Fact]
    public async Task Handle_Should_Block_Multiple_Slots_When_Existing_Service_Is_Longer()
    {
        var date = FutureDate();
        var query = new GetAvailableSlotsQuery(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), date);

        SetupWorkingHours();
        SetupQueryServiceDuration(30);

        var existingServiceId = Guid.NewGuid();
        var existingAppt = Appointment.Create(
            query.ClinicId, Guid.NewGuid(), Guid.NewGuid(), existingServiceId, query.VeterinarianId,
            date, new TimeOnly(8, 30), null, Guid.NewGuid());

        SetupExistingDurations(new Dictionary<Guid, int> { [existingServiceId] = 90 });
        SetupAppointments([existingAppt]);

        var result = await _handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEquivalentTo(new List<string> { "08:00" });
    }

    [Fact]
    public async Task Handle_Should_Return_Failure_When_Service_Duration_Is_Invalid()
    {
        var query = new GetAvailableSlotsQuery(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), FutureDate());

        SetupQueryServiceDuration(0);

        var result = await _handler.Handle(query, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("appointments.slots.invalid_duration");
    }
}