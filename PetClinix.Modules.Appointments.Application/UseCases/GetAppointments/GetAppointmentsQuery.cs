using PetClinix.BuildingBlocks.Application;

namespace PetClinix.Modules.Appointments.Application.UseCases.GetAppointments;

public sealed record GetAppointmentsQuery(Guid ClinicId, DateOnly Date, int PageNumber, int PageSize) : ICommand<Result<PagedResult<AppointmentResponse>>>;