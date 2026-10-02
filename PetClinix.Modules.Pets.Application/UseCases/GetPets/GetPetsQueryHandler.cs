using PetClinix.BuildingBlocks.Application;
using PetClinix.Modules.Pets.Domain.Repositories;

namespace PetClinix.Modules.Pets.Application.UseCases.GetPets;

public sealed class GetPetsQueryHandler : ICommandHandler<GetPetsQuery, Result<PagedResult<PetResponse>>>
{
    private readonly IPetRepository _petRepository;

    public GetPetsQueryHandler(IPetRepository petRepository)
    {
        _petRepository = petRepository;
    }

    public async Task<Result<PagedResult<PetResponse>>> Handle(GetPetsQuery query, CancellationToken cancellationToken)
    {
        var (pets, totalCount) = await _petRepository.GetAllByTutorIdAsync(
            query.ClinicId, query.TutorId, query.PageNumber, query.PageSize, query.Search, cancellationToken);

        var response = pets.Select(p => new PetResponse(
            p.Id,
            p.Name,
            p.Species,
            p.Breed,
            p.BirthDate,
            p.Sex,
            p.Weight,
            p.IsNeutered)).ToList();

        var pagedResult = new PagedResult<PetResponse>(response, totalCount, query.PageNumber, query.PageSize);

        return Result<PagedResult<PetResponse>>.Success(pagedResult);
    }
}