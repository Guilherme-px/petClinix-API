namespace PetClinix.Modules.Appointments.Application.Contracts;

public interface IServiceCatalogService
{
    Task<int> GetDurationInMinutesAsync(Guid clinicId, Guid serviceId, CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<Guid, int>> GetDurationsInMinutesAsync(Guid clinicId, IReadOnlyCollection<Guid> serviceIds, CancellationToken cancellationToken = default);
}