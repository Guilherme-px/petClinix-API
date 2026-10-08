using PetClinix.BuildingBlocks.Application;
using PetClinix.Modules.Appointments.Domain.Repositories;

namespace PetClinix.Modules.Appointments.Application.UseCases.GetAppointments;

public sealed class GetAppointmentsQueryHandler : ICommandHandler<GetAppointmentsQuery, Result<List<AppointmentResponse>>>
{
    private readonly IAppointmentRepository _appointmentRepository;

    public GetAppointmentsQueryHandler(IAppointmentRepository appointmentRepository)
    {
        _appointmentRepository = appointmentRepository;
    }

    public async Task<Result<List<AppointmentResponse>>> Handle(GetAppointmentsQuery query, CancellationToken cancellationToken)
    {
        if (query.StartDate > query.EndDate)
            return Result<List<AppointmentResponse>>.Failure("appointments.appt.invalid_range", "A data inicial deve ser anterior à data final.");

        var appointments = await _appointmentRepository.GetAllByClinicAndDateRangeAsync(query.ClinicId, query.StartDate, query.EndDate, cancellationToken);

        var response = appointments.Select(a => new AppointmentResponse(
            a.Id,
            a.TutorId,
            a.PetId,
            a.ServiceId,
            a.VeterinarianId,
            a.ScheduledDate,
            a.ScheduledTime,
            a.Notes,
            a.Status)).ToList();

        return Result<List<AppointmentResponse>>.Success(response);
    }
}