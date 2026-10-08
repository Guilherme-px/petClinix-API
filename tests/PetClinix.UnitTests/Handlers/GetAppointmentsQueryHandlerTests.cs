using FluentAssertions;
using NSubstitute;
using PetClinix.Modules.Appointments.Application.UseCases.GetAppointments;
using PetClinix.Modules.Appointments.Domain.Entities;
using PetClinix.Modules.Appointments.Domain.Repositories;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace PetClinix.UnitTests.Handlers;

public class GetAppointmentsQueryHandlerTests
{
    private readonly IAppointmentRepository _appointmentRepositoryMock;
    private readonly GetAppointmentsQueryHandler _handler;

    public GetAppointmentsQueryHandlerTests()
    {
        _appointmentRepositoryMock = Substitute.For<IAppointmentRepository>();
        _handler = new GetAppointmentsQueryHandler(_appointmentRepositoryMock);
    }

    private static Appointment CreateValidAppointment(Guid clinicId, DateOnly date)
    {
        return Appointment.Create(
            clinicId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            date, new TimeOnly(10, 0), "Teste", Guid.NewGuid()
        );
    }

    [Fact]
    public async Task Handle_Should_ReturnFailure_When_StartDate_Is_After_EndDate()
    {
        var start = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(2));
        var query = new GetAppointmentsQuery(Guid.NewGuid(), start, start.AddDays(-1));

        var result = await _handler.Handle(query, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("appointments.appt.invalid_range");
    }

    [Fact]
    public async Task Handle_Should_Return_All_Appointments_In_Range()
    {
        var clinicId = Guid.NewGuid();
        var start = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1));
        var end = start.AddDays(7);

        var appointments = new List<Appointment>
        {
            CreateValidAppointment(clinicId, start),
            CreateValidAppointment(clinicId, end)
        };

        _appointmentRepositoryMock.GetAllByClinicAndDateRangeAsync(clinicId, start, end, Arg.Any<CancellationToken>())
            .Returns(appointments);

        var result = await _handler.Handle(new GetAppointmentsQuery(clinicId, start, end), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Should().HaveCount(2);
    }

    [Fact]
    public async Task Handle_Should_Return_Empty_List_When_No_Appointments_In_Range()
    {
        var clinicId = Guid.NewGuid();
        var start = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1));
        var query = new GetAppointmentsQuery(clinicId, start, start.AddDays(7));

        _appointmentRepositoryMock.GetAllByClinicAndDateRangeAsync(clinicId, start, start.AddDays(7), Arg.Any<CancellationToken>())
            .Returns(new List<Appointment>());

        var result = await _handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Should().BeEmpty();
    }
}