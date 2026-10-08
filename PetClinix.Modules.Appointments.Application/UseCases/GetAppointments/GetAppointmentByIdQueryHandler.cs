using PetClinix.BuildingBlocks.Application;
using PetClinix.Modules.Appointments.Domain.Repositories;

namespace PetClinix.Modules.Appointments.Application.UseCases.GetAppointments;

public sealed class GetAppointmentByIdQueryHandler : ICommandHandler<GetAppointmentByIdQuery, Result<AppointmentResponse>>
{
    private readonly IAppointmentRepository _appointmentRepository;

    public GetAppointmentByIdQueryHandler(IAppointmentRepository appointmentRepository)
    {
        _appointmentRepository = appointmentRepository;
    }

    public async Task<Result<AppointmentResponse>> Handle(GetAppointmentByIdQuery query, CancellationToken cancellationToken)
    {
        var appointment = await _appointmentRepository.GetByIdAsync(query.AppointmentId, cancellationToken);

        if (appointment == null || appointment.ClinicId != query.ClinicId)
        {
            return Result<AppointmentResponse>.Failure("appointments.appt.not_found", "Agendamento não encontrado.");
        }

        var response = new AppointmentResponse(
            appointment.Id,
            appointment.TutorId,
            appointment.PetId,
            appointment.ServiceId,
            appointment.VeterinarianId,
            appointment.ScheduledDate,
            appointment.ScheduledTime,
            appointment.Notes,
            appointment.Status
        );

        return Result<AppointmentResponse>.Success(response);
    }
}