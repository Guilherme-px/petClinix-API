using PetClinix.BuildingBlocks.Application;
using PetClinix.Modules.Catalog.Domain.Repositories;

namespace PetClinix.Modules.Catalog.Application.UseCases.GetServices;

public sealed class GetServicesQueryHandler : ICommandHandler<GetServicesQuery, Result<PagedResult<ServiceResponse>>>
{
    private readonly IServiceRepository _serviceRepository;

    public GetServicesQueryHandler(IServiceRepository serviceRepository)
    {
        _serviceRepository = serviceRepository;
    }

    public async Task<Result<PagedResult<ServiceResponse>>> Handle(GetServicesQuery query, CancellationToken cancellationToken)
    {
        var (services, totalCount) = await _serviceRepository.GetAllByClinicIdAsync(
            query.ClinicId, query.PageNumber, query.PageSize, query.Search, cancellationToken);

        var response = services.Select(s => new ServiceResponse(
            s.Id,
            s.Name,
            s.Description,
            s.DurationInMinutes,
            s.Price,
            s.RequiresVeterinarian)).ToList();

        var pagedResult = new PagedResult<ServiceResponse>(response, totalCount, query.PageNumber, query.PageSize);

        return Result<PagedResult<ServiceResponse>>.Success(pagedResult);
    }
}