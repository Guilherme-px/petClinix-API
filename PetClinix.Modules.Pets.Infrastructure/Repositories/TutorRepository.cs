using Microsoft.EntityFrameworkCore;
using PetClinix.BuildingBlocks.Infrastructure;
using PetClinix.Modules.Pets.Domain.Entities;
using PetClinix.Modules.Pets.Domain.Repositories;
using PetClinix.Modules.Pets.Domain.ValueObjects;
using PetClinix.Modules.Pets.Infrastructure.Persistence;

namespace PetClinix.Modules.Pets.Infrastructure.Repositories;

public class TutorRepository : ITutorRepository
{
    private readonly PetsDbContext _context;

    public TutorRepository(PetsDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(Tutor tutor, CancellationToken cancellationToken = default)
    {
        await _context.Tutors.AddAsync(tutor, cancellationToken);
    }

    public async Task<bool> ExistsByCpfAsync(Guid clinicId, Cpf cpf, CancellationToken cancellationToken = default)
    {
        return await _context.Tutors.AnyAsync(t => t.ClinicId == clinicId && t.Cpf == cpf, cancellationToken);
    }

    public async Task<(IEnumerable<Tutor> Tutors, int TotalCount)> GetAllByClinicIdAsync(
        Guid clinicId, int pageNumber, int pageSize, string search = "",
        CancellationToken cancellationToken = default)
    {
        var query = _context.Tutors
            .Where(t => t.ClinicId == clinicId && t.IsActive);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = TextSearch.Normalize(search);
            query = query.Where(t => EF.Functions.ILike(
                EF.Property<string>(t, "NameSearchable"), $"%{term}%"));
        }

        query = query.OrderBy(t => t.Name);

        var totalCount = await query.CountAsync(cancellationToken);

        var tutors = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (tutors, totalCount);
    }

    public async Task<Tutor?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Tutors.FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
    }

    public async Task UpdateAsync(Tutor tutor, CancellationToken cancellationToken = default)
    {
        _context.Tutors.Update(tutor);
    }
}