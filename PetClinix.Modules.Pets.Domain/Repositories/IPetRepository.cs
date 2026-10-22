using PetClinix.Modules.Pets.Domain.Entities;

namespace PetClinix.Modules.Pets.Domain.Repositories;

public interface IPetRepository
{
    Task AddAsync(Pet pet, CancellationToken cancellationToken = default);
    Task<Pet?> GetByIdAsync(Guid clinicId, Guid id, CancellationToken cancellationToken = default);
    Task<(IEnumerable<Pet> Pets, int TotalCount)> GetAllByTutorIdAsync(
        Guid clinicId, Guid tutorId, int pageNumber, int pageSize, string search = "", CancellationToken cancellationToken = default);
    Task<bool> ExistsByNameAndTutorAsync(Guid clinicId, Guid tutorId, string name, CancellationToken cancellationToken = default);
    Task UpdateAsync(Pet pet, CancellationToken cancellationToken = default);
    Task<bool> ExistsActiveByTutorIdAsync(Guid clinicId, Guid tutorId, CancellationToken cancellationToken = default);
}