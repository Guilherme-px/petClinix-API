using PetClinix.BuildingBlocks.Application;

namespace PetClinix.Modules.Pets.Application.UseCases.GetPets;

public sealed record GetPetsQuery(Guid ClinicId, Guid TutorId, int PageNumber, int PageSize, string Search = "") : ICommand<Result<PagedResult<PetResponse>>>;