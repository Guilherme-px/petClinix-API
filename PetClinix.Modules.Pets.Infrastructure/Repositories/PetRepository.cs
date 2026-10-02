using Microsoft.EntityFrameworkCore;
using PetClinix.BuildingBlocks.Infrastructure;
using PetClinix.Modules.Pets.Domain.Entities;
using PetClinix.Modules.Pets.Domain.Repositories;
using PetClinix.Modules.Pets.Infrastructure.Persistence;

namespace PetClinix.Modules.Pets.Infrastructure.Repositories;

public class PetRepository : IPetRepository
{
    private readonly PetsDbContext _context;

    public PetRepository(PetsDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(Pet pet, CancellationToken cancellationToken = default)
    {
        await _context.Pets.AddAsync(pet, cancellationToken);
    }

    public async Task<bool> ExistsByNameAndTutorAsync(Guid tutorId, string name, CancellationToken cancellationToken = default)
    {
        return await _context.Pets.AnyAsync(p => p.TutorId == tutorId && p.Name == name, cancellationToken);
    }

    public async Task<Pet?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Pets.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
    }

    public async Task<(IEnumerable<Pet> Pets, int TotalCount)> GetAllByTutorIdAsync(
        Guid clinicId,
        Guid tutorId,
        int pageNumber,
        int pageSize,
        string search = "",
        CancellationToken cancellationToken = default)
    {
        var query = _context.Pets
           .Where(p => p.ClinicId == clinicId && p.TutorId == tutorId && p.IsActive);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = TextSearch.Normalize(search);
            query = query.Where(p => EF.Functions.ILike(
                EF.Property<string>(p, "NameSearchable"), $"%{term}%"));
        }

        query = query.OrderBy(p => p.Name);

        var totalCount = await query.CountAsync(cancellationToken);

        var pets = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (pets, totalCount);
    }

    public async Task UpdateAsync(Pet pet, CancellationToken cancellationToken = default)
    {
        _context.Pets.Update(pet);
    }

    public async Task<bool> ExistsActiveByTutorIdAsync(Guid tutorId, CancellationToken cancellationToken = default)
    {
        return await _context.Pets.AnyAsync(p => p.TutorId == tutorId && p.IsActive, cancellationToken);
    }
}