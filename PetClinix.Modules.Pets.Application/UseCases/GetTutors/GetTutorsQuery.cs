using PetClinix.BuildingBlocks.Application;

namespace PetClinix.Modules.Pets.Application.UseCases.GetTutors;

public sealed record GetTutorsQuery(Guid ClinicId, int PageNumber, int PageSize, string Search = "") : ICommand<Result<PagedResult<TutorResponse>>>;