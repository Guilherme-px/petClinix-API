namespace PetClinix.Modules.Appointments.Application.Contracts;

public interface IAppointmentReferenceChecker
{
    Task<bool> TutorExistsInClinicAsync(Guid clinicId, Guid tutorId, CancellationToken cancellationToken = default);
    Task<bool> PetExistsInClinicAsync(Guid clinicId, Guid petId, CancellationToken cancellationToken = default);
    Task<bool> VeterinarianExistsInClinicAsync(Guid clinicId, Guid veterinarianId, CancellationToken cancellationToken = default);
}