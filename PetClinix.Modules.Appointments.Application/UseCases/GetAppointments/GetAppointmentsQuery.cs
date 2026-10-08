using PetClinix.BuildingBlocks.Application;

namespace PetClinix.Modules.Appointments.Application.UseCases.GetAppointments;

public sealed record GetAppointmentsQuery(Guid ClinicId, DateOnly StartDate, DateOnly EndDate) : ICommand<Result<List<AppointmentResponse>>>;