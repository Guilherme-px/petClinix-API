using PetClinix.BuildingBlocks.Application;

namespace PetClinix.Modules.Catalog.Application.UseCases.GetServices;

public sealed record GetServicesQuery(Guid ClinicId, int PageNumber, int PageSize, string Search = "") : ICommand<Result<PagedResult<ServiceResponse>>>;