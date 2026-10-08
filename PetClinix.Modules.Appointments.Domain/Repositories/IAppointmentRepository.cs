using PetClinix.Modules.Appointments.Domain.Entities;

namespace PetClinix.Modules.Appointments.Domain.Repositories;

public interface IAppointmentRepository
{
    Task AddAsync(Appointment appointment, CancellationToken cancellationToken = default);
    Task<Appointment?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<List<Appointment>> GetByVeterinarianAndDateAsync(Guid veterinarianId, DateOnly date, CancellationToken cancellationToken = default);
    Task<(IEnumerable<Appointment> Appointments, int TotalCount)> GetAllByClinicAndDateAsync(Guid clinicId, DateOnly date, int pageNumber, int pageSize, CancellationToken cancellationToken = default);
    Task UpdateAsync(Appointment appointment, CancellationToken cancellationToken = default);
}