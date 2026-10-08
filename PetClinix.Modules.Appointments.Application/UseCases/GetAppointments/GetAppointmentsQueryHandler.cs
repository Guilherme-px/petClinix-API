using PetClinix.BuildingBlocks.Application;
using PetClinix.Modules.Appointments.Domain.Repositories;

namespace PetClinix.Modules.Appointments.Application.UseCases.GetAppointments;

public sealed class GetAppointmentsQueryHandler : ICommandHandler<GetAppointmentsQuery, Result<PagedResult<AppointmentResponse>>>
{
    private readonly IAppointmentRepository _appointmentRepository;

    public GetAppointmentsQueryHandler(IAppointmentRepository appointmentRepository)
    {
        _appointmentRepository = appointmentRepository;
    }

    public async Task<Result<PagedResult<AppointmentResponse>>> Handle(GetAppointmentsQuery query, CancellationToken cancellationToken)
    {
        var (appointments, totalCount) = await _appointmentRepository.GetAllByClinicAndDateAsync(query.ClinicId, query.Date, query.PageNumber, query.PageSize, cancellationToken);

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

        var pagedResult = new PagedResult<AppointmentResponse>(response, totalCount, query.PageNumber, query.PageSize);

        return Result<PagedResult<AppointmentResponse>>.Success(pagedResult);
    }
}