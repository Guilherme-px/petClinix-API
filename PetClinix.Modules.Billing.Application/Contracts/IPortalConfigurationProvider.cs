namespace PetClinix.Modules.Billing.Application.Contracts;

public interface IPortalConfigurationProvider
{
    Task<string?> GetOrCreateConfigurationIdAsync(CancellationToken cancellationToken = default);
}