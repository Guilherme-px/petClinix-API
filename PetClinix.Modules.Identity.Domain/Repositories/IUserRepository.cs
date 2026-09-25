using PetClinix.Modules.Identity.Domain.Entities;
using PetClinix.Modules.Identity.Domain.ValueObjects;

namespace PetClinix.Modules.Identity.Domain.Repositories;

public interface IUserRepository
{
    Task AddAsync(User user, CancellationToken cancellationToken = default);
    Task<bool> ExistsByEmailAsync(Email email, CancellationToken cancellationToken = default);
    Task<User?> GetByPasswordResetTokenAsync(string token, CancellationToken cancellationToken = default);
    Task<User?> GetByEmailAsync(Email email, CancellationToken cancellationToken = default);
    Task UpdateAsync(User user, CancellationToken cancellationToken = default);
    Task<User?> GetByRefreshTokenAsync(string token, CancellationToken cancellationToken = default);
    Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<int> CountByClinicIdAsync(Guid clinicId, CancellationToken cancellationToken = default);
    Task<(IEnumerable<User> Users, int TotalCount)> GetAllByClinicIdAsync(
        Guid clinicId,
        int pageNumber,
        int pageSize,
        string search = "",
        CancellationToken cancellationToken = default);
}