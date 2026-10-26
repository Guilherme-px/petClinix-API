namespace PetClinix.BuildingBlocks.Application;

public interface IAppointmentDependencyChecker
{
    Task<bool> HasFutureAppointmentsForTutorAsync(Guid clinicId, Guid tutorId, CancellationToken cancellationToken = default);
    Task<bool> HasFutureAppointmentsForPetAsync(Guid clinicId, Guid petId, CancellationToken cancellationToken = default);
    Task<bool> HasFutureAppointmentsForVetAsync(Guid clinicId, Guid vetId, CancellationToken cancellationToken = default);
    Task<bool> HasFutureAppointmentsForServiceAsync(Guid clinicId, Guid serviceId, CancellationToken cancellationToken = default);
}