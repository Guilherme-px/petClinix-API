using Microsoft.EntityFrameworkCore;
using PetClinix.Modules.Appointments.Domain.Entities;
using PetClinix.Modules.Appointments.Domain.Enums;
using PetClinix.Modules.Appointments.Domain.Repositories;
using PetClinix.Modules.Appointments.Infrastructure.Persistence;

namespace PetClinix.Modules.Appointments.Infrastructure.Repositories;

public class AppointmentRepository : IAppointmentRepository
{
    private readonly AppointmentsDbContext _context;

    public AppointmentRepository(AppointmentsDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(Appointment appointment, CancellationToken cancellationToken = default)
    {
        await _context.Appointments.AddAsync(appointment, cancellationToken);
    }

    public async Task<Appointment?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Appointments.FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
    }

    public async Task<List<Appointment>> GetByVeterinarianAndDateAsync(Guid veterinarianId, DateOnly date, CancellationToken cancellationToken = default)
    {
        return await _context.Appointments
            .Where(a => a.VeterinarianId == veterinarianId &&
                        a.ScheduledDate == date &&
                        a.Status != AppointmentStatus.Canceled)
            .OrderBy(a => a.ScheduledTime)
            .ToListAsync(cancellationToken);
    }

    public async Task<(IEnumerable<Appointment> Appointments, int TotalCount)> GetAllByClinicAndDateAsync(Guid clinicId, DateOnly date, int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = _context.Appointments
            .Where(a => a.ClinicId == clinicId && a.ScheduledDate == date)
            .OrderBy(a => a.ScheduledTime);

        var totalCount = await query.CountAsync(cancellationToken);

        var appointments = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (appointments, totalCount);
    }

    public async Task UpdateAsync(Appointment appointment, CancellationToken cancellationToken = default)
    {
        _context.Appointments.Update(appointment);
    }
}