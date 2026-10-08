using PetClinix.BuildingBlocks.Application;

namespace PetClinix.Modules.Appointments.Application.UseCases.UpdateAppointment;

public sealed record UpdateAppointmentCommand(
    Guid ClinicId, Guid AppointmentId, Guid UpdatedByUserId,
    Guid VeterinarianId, Guid ServiceId, DateOnly Date, TimeOnly Time, string? Notes
) : ICommand<Result>;