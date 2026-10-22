namespace PetClinix.Modules.Pets.Application.Contracts;

public interface IPetDependencyChecker
{
    Task<bool> HasActivePetsByTutorAsync(Guid clinicId, Guid tutorId, CancellationToken cancellationToken = default);
}