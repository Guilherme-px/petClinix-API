using Microsoft.EntityFrameworkCore;
using PetClinix.BuildingBlocks.Infrastructure;
using PetClinix.Modules.Identity.Domain.Entities;
using PetClinix.Modules.Identity.Domain.Enums;
using PetClinix.Modules.Identity.Domain.Repositories;
using PetClinix.Modules.Identity.Domain.ValueObjects;
using PetClinix.Modules.Identity.Infrastructure.Persistence;

namespace PetClinix.Modules.Identity.Infrastructure.Repositories;

public class UserRepository : IUserRepository
{
    private readonly IdentityDbContext _context;

    public UserRepository(IdentityDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(User user, CancellationToken cancellationToken = default)
    {
        await _context.Users.AddAsync(user, cancellationToken);
    }

    public async Task<bool> ExistsByEmailAsync(Email email, CancellationToken cancellationToken = default)
    {
        return await _context.Users.AnyAsync(u => u.Email == email, cancellationToken);
    }

    public async Task<User?> GetByPasswordResetTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        return await _context.Users.FirstOrDefaultAsync(u => u.PasswordResetToken == token, cancellationToken);
    }

    public async Task<User?> GetByEmailAsync(Email email, CancellationToken cancellationToken = default)
    {
        return await _context.Users.FirstOrDefaultAsync(u => u.Email == email, cancellationToken);
    }

    public async Task UpdateAsync(User user, CancellationToken cancellationToken = default)
    {
        _context.Users.Update(user);
    }

    public async Task<User?> GetByRefreshTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        return await _context.Users.FirstOrDefaultAsync(u => u.RefreshToken == token, cancellationToken);
    }

    public async Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Users.FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
    }

    public async Task<int> CountByClinicIdAsync(Guid clinicId, CancellationToken cancellationToken = default)
    {
        return await _context.Users.CountAsync(u => u.ClinicId == clinicId, cancellationToken);
    }

    public async Task<(IEnumerable<User> Users, int TotalCount)> GetAllByClinicIdAsync(
        Guid clinicId, int pageNumber, int pageSize, string search = "", CancellationToken cancellationToken = default)
    {
        var query = _context.Users
            .Where(u => u.ClinicId == clinicId && u.Role != UserRole.Admin);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = TextSearch.Normalize(search);
            query = query.Where(u => EF.Functions.ILike(
                EF.Property<string>(u, "NameSearchable"), $"%{term}%"));
        }

        query = query.OrderBy(u => u.Name);

        var totalCount = await query.CountAsync(cancellationToken);

        var users = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (users, totalCount);
    }
}