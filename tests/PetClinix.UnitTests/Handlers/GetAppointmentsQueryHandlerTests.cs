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
    public async Task Handle_Should_Return_PagedResult_With_Correct_Data()
    {
        var clinicId = Guid.NewGuid();
        var date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1));
        var query = new GetAppointmentsQuery(clinicId, date, 1, 10);

        var appointments = new List<Appointment>
        {
            CreateValidAppointment(clinicId, date),
            CreateValidAppointment(clinicId, date)
        };

        _appointmentRepositoryMock.GetAllByClinicAndDateAsync(clinicId, date, 1, 10, Arg.Any<CancellationToken>())
            .Returns((appointments, 2));

        var result = await _handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Items.Should().HaveCount(2);
        result.Value.TotalCount.Should().Be(2);
        result.Value.PageNumber.Should().Be(1);
        result.Value.PageSize.Should().Be(10);
    }

    [Fact]
    public async Task Handle_Should_Return_Empty_List_When_No_Appointments_Exist()
    {
        var clinicId = Guid.NewGuid();
        var date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1));
        var query = new GetAppointmentsQuery(clinicId, date, 1, 10);

        _appointmentRepositoryMock.GetAllByClinicAndDateAsync(clinicId, date, 1, 10, Arg.Any<CancellationToken>())
            .Returns((new List<Appointment>(), 0));

        var result = await _handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Items.Should().BeEmpty();
        result.Value.TotalCount.Should().Be(0);
    }
}