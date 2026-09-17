using Microsoft.EntityFrameworkCore;
using PetClinix.Modules.Catalog.Domain.Entities;
using PetClinix.Modules.Catalog.Domain.Repositories;
using PetClinix.Modules.Catalog.Infrastructure.Persistence;

namespace PetClinix.Modules.Catalog.Infrastructure.Repositories;

public class ServiceRepository : IServiceRepository
{
    private readonly CatalogDbContext _context;

    public ServiceRepository(CatalogDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(Service service, CancellationToken cancellationToken = default)
    {
        await _context.Services.AddAsync(service, cancellationToken);
    }

    public async Task<bool> ExistsByNameAsync(Guid clinicId, string name, CancellationToken cancellationToken = default)
    {
        return await _context.Services.AnyAsync(s => s.ClinicId == clinicId && s.Name == name, cancellationToken);
    }

    public async Task<(IEnumerable<Service> Services, int TotalCount)> GetAllByClinicIdAsync(
    Guid clinicId, int pageNumber, int pageSize, string search = "", CancellationToken cancellationToken = default)
    {
        var query = _context.Services
            .Where(s => s.ClinicId == clinicId && s.IsActive);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(s => s.Name.ToLower().Contains(term.ToLower()));
        }

        query = query.OrderBy(s => s.Name);

        var totalCount = await query.CountAsync(cancellationToken);

        var services = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (services, totalCount);
    }

    public async Task<Service?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Services.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
    }

    public async Task UpdateAsync(Service service, CancellationToken cancellationToken = default)
    {
        _context.Services.Update(service);
    }
}