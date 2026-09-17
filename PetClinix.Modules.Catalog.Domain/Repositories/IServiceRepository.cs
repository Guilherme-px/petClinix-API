using PetClinix.Modules.Catalog.Domain.Entities;

namespace PetClinix.Modules.Catalog.Domain.Repositories;

public interface IServiceRepository
{
    Task AddAsync(Service service, CancellationToken cancellationToken = default);
    Task<(IEnumerable<Service> Services, int TotalCount)> GetAllByClinicIdAsync(Guid clinicId,
        int pageNumber,
        int pageSize,
        string search = "",
        CancellationToken cancellationToken = default);
    Task<Service?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> ExistsByNameAsync(Guid clinicId, string name, CancellationToken cancellationToken = default);
    Task UpdateAsync(Service service, CancellationToken cancellationToken = default);
}