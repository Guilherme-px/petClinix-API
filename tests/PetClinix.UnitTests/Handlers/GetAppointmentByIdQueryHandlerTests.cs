using FluentAssertions;
using NSubstitute;
using PetClinix.Modules.Appointments.Application.UseCases.GetAppointments;
using PetClinix.Modules.Appointments.Domain.Entities;
using PetClinix.Modules.Appointments.Domain.Repositories;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace PetClinix.UnitTests.Handlers;

public class GetAppointmentByIdQueryHandlerTests
{
    private readonly IAppointmentRepository _appointmentRepositoryMock;
    private readonly GetAppointmentByIdQueryHandler _handler;

    public GetAppointmentByIdQueryHandlerTests()
    {
        _appointmentRepositoryMock = Substitute.For<IAppointmentRepository>();
        _handler = new GetAppointmentByIdQueryHandler(_appointmentRepositoryMock);
    }

    private static Appointment CreateValidAppointment(Guid clinicId)
    {
        return Appointment.Create(
            clinicId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)), new TimeOnly(10, 0), "Teste", Guid.NewGuid()
        );
    }

    [Fact]
    public async Task Handle_Should_ReturnFailure_When_Appointment_Does_Not_Exist()
    {
        var query = new GetAppointmentByIdQuery(Guid.NewGuid(), Guid.NewGuid());

        _appointmentRepositoryMock.GetByIdAsync(query.AppointmentId, Arg.Any<CancellationToken>()).Returns((Appointment?)null);

        var result = await _handler.Handle(query, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("appointments.appt.not_found");
    }

    [Fact]
    public async Task Handle_Should_ReturnFailure_When_Appointment_Belongs_To_Another_Clinic()
    {
        var myClinicId = Guid.NewGuid();
        var otherClinicId = Guid.NewGuid();
        var appointment = CreateValidAppointment(otherClinicId);
        var query = new GetAppointmentByIdQuery(myClinicId, appointment.Id);

        _appointmentRepositoryMock.GetByIdAsync(query.AppointmentId, Arg.Any<CancellationToken>()).Returns(appointment);

        var result = await _handler.Handle(query, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("appointments.appt.not_found");
    }

    [Fact]
    public async Task Handle_Should_ReturnSuccess_When_Appointment_Belongs_To_Clinic()
    {
        var clinicId = Guid.NewGuid();
        var appointment = CreateValidAppointment(clinicId);
        var query = new GetAppointmentByIdQuery(clinicId, appointment.Id);

        _appointmentRepositoryMock.GetByIdAsync(query.AppointmentId, Arg.Any<CancellationToken>()).Returns(appointment);

        var result = await _handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Id.Should().Be(appointment.Id);
        result.Value.Notes.Should().Be(appointment.Notes);
    }
}