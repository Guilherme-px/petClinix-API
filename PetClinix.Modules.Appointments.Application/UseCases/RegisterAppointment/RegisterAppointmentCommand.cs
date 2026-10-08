using PetClinix.BuildingBlocks.Application;

namespace PetClinix.Modules.Appointments.Application.UseCases.RegisterAppointment;

public sealed record RegisterAppointmentCommand(
    Guid ClinicId, Guid TutorId, Guid PetId, Guid ServiceId, Guid VeterinarianId,
    DateOnly Date, TimeOnly Time, string? Notes, Guid CreatedByUserId
) : ICommand<Result>;