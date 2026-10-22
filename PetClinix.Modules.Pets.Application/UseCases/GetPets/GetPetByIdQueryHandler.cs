using PetClinix.BuildingBlocks.Application;
using PetClinix.Modules.Pets.Domain.Repositories;

namespace PetClinix.Modules.Pets.Application.UseCases.GetPets;

public sealed class GetPetByIdQueryHandler : ICommandHandler<GetPetByIdQuery, Result<PetResponse>>
{
    private readonly IPetRepository _petRepository;

    public GetPetByIdQueryHandler(IPetRepository petRepository)
    {
        _petRepository = petRepository;
    }

    public async Task<Result<PetResponse>> Handle(GetPetByIdQuery query, CancellationToken cancellationToken)
    {
        var pet = await _petRepository.GetByIdAsync(query.ClinicId, query.PetId, cancellationToken);
        
        if (pet == null || pet.TutorId != query.TutorId)
        {
            return Result<PetResponse>.Failure("pets.pet.not_found", "Pet não encontrado para este tutor.");
        }

        var response = new PetResponse(
            pet.Id,
            pet.Name,
            pet.Species,
            pet.Breed,
            pet.BirthDate,
            pet.Sex,
            pet.Weight,
            pet.IsNeutered
        );

        return Result<PetResponse>.Success(response);
    }
}