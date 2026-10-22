using PetClinix.Modules.Pets.Application.Contracts;
using PetClinix.Modules.Pets.Domain.Repositories;

namespace PetClinix.Modules.Pets.Infrastructure.Services;

public class PetDependencyChecker : IPetDependencyChecker
{
    private readonly IPetRepository _petRepository;

    public PetDependencyChecker(IPetRepository petRepository)
    {
        _petRepository = petRepository;
    }

    public async Task<bool> HasActivePetsByTutorAsync(Guid clinicId, Guid tutorId, CancellationToken cancellationToken = default)
    {
        return await _petRepository.ExistsActiveByTutorIdAsync(clinicId, tutorId, cancellationToken);
    }
}